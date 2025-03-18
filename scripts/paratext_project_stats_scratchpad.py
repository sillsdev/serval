# %%
import json
import os

import clearml_stats
import numpy as np
import pandas as pd
from matplotlib import pyplot as plt
from paratext_project_stats import *

# %%
stats = clearml_stats.clearml_stats()
stats.update_tasks_and_projects()
stats.create_language_projects()

# %%
pp_folder = os.environ["PARATEXT_PROJECT_STATS_FOLDER"]
folders = os.listdir(pp_folder)

verses_read_in = False
verse_count_df = None
book_count_df = None

for folder in folders[:1]:
    root_path = os.path.join(pp_folder, folder, "local/stats")
    # read in the file meta.json in each folder
    meta = json.load(open(os.path.join(root_path, "meta.json")))
    revision_df = pd.DataFrame(
        json.load(open(os.path.join(root_path, "revisionStatus.json")))
    ).T
    cc_df = pd.DataFrame(
        json.load(open(os.path.join(root_path, "characterCount.json")))
    )
    notes_df = pd.DataFrame(
        json.load(open(os.path.join(root_path, "openNotesCount.json")))
    )
    cc_df = build_character_data(
        cc_df,
    )

    book_df = build_book_data(cc_df, notes_df, revision_df)
    book_df = correct_time(book_df)

    if not verses_read_in:
        verses_df = pd.DataFrame(
            json.load(open(os.path.join(root_path, "verses.json")))
        )
        book_count_df = build_verse_count_df(verses_df)
        verses_read_in = True

    book_df = book_df.merge(book_count_df, on="book", how="outer", sort="dateTime")
    book_df.reset_index(drop=True, inplace=True)
    book_df.to_csv(os.path.join(root_path, "book_data.csv"), index=False)

# %%
plt.plot(cc_df["dateTime"], cc_df["contentTotal"])

# %%
