# %%
import json
import os

import clearml_stats
import pandas as pd
from matplotlib import pyplot as plt

# %%
stats = clearml_stats.clearml_stats()
stats.update_tasks_and_projects()
stats.create_language_projects()


# %%
def build_character_data(cc_df: pd.DataFrame):
    cc_df["verse"] = cc_df["referenceNumber"] % 1000
    cc_df["chapter"] = (cc_df["referenceNumber"] // 1000) % 1000
    cc_df["book"] = cc_df["referenceNumber"] // 1000000
    cc_df["dateTime"] = pd.to_datetime(cc_df["date"] * 1000000000)
    cc_df["firstDraft"] = (cc_df["netChange"] > (cc_df["total"] - 5)) & (
        cc_df["total"] > 5
    )
    cc_df["contentTotal"] = (
        cc_df["total"]
        - cc_df["wsTotal"]
        - cc_df["punctuationTotal"]
        - cc_df["markersTotal"]
    )
    cc_df["contentChange"] = (
        cc_df["netChange"]
        - cc_df["wsNetChange"]
        - cc_df["punctuationNetChange"]
        - cc_df["markersNetChange"]
    )
    cc_df["minorEdit"] = cc_df["contentChange"] < 5
    cc_df["moderateEdit"] = (cc_df["contentChange"] >= 5) & (
        cc_df["contentChange"] < (0.5 * cc_df["contentTotal"])
    )
    cc_df["majorEdit"] = (cc_df["contentChange"] >= 5) & (
        cc_df["contentChange"] >= (0.5 * cc_df["contentTotal"])
    )
    return cc_df


def build_verse_count_df(verses_df: pd.DataFrame):
    verses_df["verse"] = verses_df["referenceNumber"] % 1000
    verses_df["chapter"] = (verses_df["referenceNumber"] // 1000) % 1000
    verses_df["book"] = verses_df["referenceNumber"] // 1000000
    verse_count_df = (
        verses_df[["book", "chapter", "verse"]].groupby(["book", "chapter"]).count()
    )
    book_count_df = verses_df[["book", "verse"]].groupby(["book"]).count()
    return verse_count_df, book_count_df


# %% read in env variable

pp_folder = os.environ["PARATEXT_PROJECT_STATS_FOLDER"]
folders = os.listdir(pp_folder)

verses_read_in = False
verse_count_df = None
book_count_df = None

for folder in folders[:1]:
    root_path = os.path.join(pp_folder, folder, "local/stats")
    # read in the file meta.json in each folder
    meta = json.load(open(os.path.join(root_path, "meta.json")))
    cc_df = pd.DataFrame(
        json.load(open(os.path.join(root_path, "characterCount.json")))
    )
    cc_df = build_character_data(cc_df)
    if not verses_read_in:
        verses_df = pd.DataFrame(
            json.load(open(os.path.join(root_path, "verses.json")))
        )
        verse_count_df, book_count_df = build_verse_count_df(verses_df)
        verses_read_in = True

# %%
plt.plot(cc_df["dateTime"], cc_df["contentTotal"])

# %%
