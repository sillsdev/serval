# %%
import os
import time

import pandas as pd
from serval_auth_module import ServalBearerAuth
from serval_client_module import RemoteCaller

# %%

serval_auth = ServalBearerAuth()
client = RemoteCaller(url_prefix=os.environ.get("SERVAL_HOST_URL"), auth=serval_auth)

# %%
files_df = pd.read_csv("prod_serval.data_files.files.csv")

# %%
owner = os.environ.get("SERVAL_CLIENT_ID")
print(owner)
files_df = files_df[files_df["owner"] == f"{owner}@clients"]
print(f"{len(files_df)} files with client {owner} as owner.")
files_df = files_df[files_df["name"].str.contains("[0-9]+_[0-9]+")]
num_of_files_to_delete = len(files_df)
print(f"{num_of_files_to_delete} files with name pattern [0-9]+_[0-9]+.")

# %%
for i in range(num_of_files_to_delete):
    file_id = files_df.iloc[i]["_id"]
    print(f"Deleting file {i} with id {file_id}.")
    client.data_files_get(file_id)
    # client.data_files_delete(file_id)
    # wait 0.1 seconds
    time.sleep(0.1)

# %%
