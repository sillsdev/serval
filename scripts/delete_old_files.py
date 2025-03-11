# %%
import datetime
import os
import re
import time

import pandas as pd
from bson.objectid import ObjectId
from serval_auth_module import ServalBearerAuth
from serval_client_module import RemoteCaller

# %%

serval_auth = ServalBearerAuth()
client = RemoteCaller(url_prefix=os.environ.get("SERVAL_HOST_URL"), auth=serval_auth)

# %%
files_df = pd.read_csv("prod_serval.data_files.files.csv")
files_df["timestamp"] = files_df["_id"].apply(
    lambda x: ObjectId(x).generation_time.timestamp()
)
with open("prod_serval.translation.engines.json") as f:
    contents = f.read()
    all_matches = set(re.findall(r'"filename": "([^"]*)"', contents))

with open("prod_serval.corpora.corpus.json") as f:
    corpus_contents = f.read()
    all_corpus_matches = set(re.findall(r'"\$oid": "([^"]*)"', corpus_contents))


# %%
owner = os.environ.get("SERVAL_CLIENT_ID")
print(owner)
print(f"{len(files_df)} files in total.")
files_df = files_df[files_df["owner"] == f"{owner}@clients"]
print(f"{len(files_df)} files with client {owner} as owner.")
# files over 6 months old
# files without matching filenames in engines
engine_mask = files_df["filename"].apply(lambda x: x not in all_matches)
corpora_mask = files_df["_id"].apply(lambda x: x not in all_corpus_matches)
print(
    f"{sum(engine_mask)} files without matching the {len(all_matches)} filenames in engines."
)
print(
    f"{sum(corpora_mask)} files without matching the {len(all_corpus_matches)} ids in corpus."
)
print(
    f"{sum(engine_mask & corpora_mask)} files without matching filenames in engines or corpus."
)
six_months_ago = datetime.datetime.now() - datetime.timedelta(days=180)
time_mask = files_df["timestamp"] < six_months_ago.timestamp()
print(f"{sum(time_mask)} files older than 6 months.")
files_df = files_df[time_mask & engine_mask & corpora_mask]
num_of_files_to_delete = len(files_df)
print(f"{num_of_files_to_delete} files to delete.")

# %%
for i in range(num_of_files_to_delete):
    file_id = files_df.iloc[i]["_id"]
    print(f"Deleting file {i} with id {file_id}.")
    client.data_files_delete(file_id)
    time.sleep(0.05)

print(f"Completed deletion of {num_of_files_to_delete} files.")

# %%
