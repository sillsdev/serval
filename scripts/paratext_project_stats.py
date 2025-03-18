# %%
import json
import os

import numpy as np
import pandas as pd


def load_json_data(
    root_path: str,
) -> (pd.DataFrame, pd.DataFrame, pd.DataFrame, pd.DataFrame):
    meta = json.load(open(os.path.join(root_path, "meta.json")))
    revision_df = pd.DataFrame(
        json.load(open(os.path.join(root_path, "revisionStatus.json")))
    ).T
    cc_df = pd.DataFrame(
        json.load(open(os.path.join(root_path, "characterCount.json")))
    )
    cc_df = build_character_data(cc_df)

    notes = json.load(open(os.path.join(root_path, "openNotesCount.json")))
    notes_df = pd.json_normalize(notes, record_path="byBook", meta=["date"])
    notes_df = build_notes_data(notes_df)

    return meta, revision_df, cc_df, notes_df


def build_character_data(cc_df: pd.DataFrame):
    cc_df["verse"] = cc_df["referenceNumber"] % 1000
    cc_df["chapter"] = (cc_df["referenceNumber"] // 1000) % 1000
    cc_df["book"] = cc_df["referenceNumber"] // 1000000
    cc_df["firstDraft"] = (
        (cc_df["netChange"] > (cc_df["total"] - 5)) & (cc_df["total"] > 5)
    ).astype(int)
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
    cc_df["minorEdit"] = (cc_df["contentChange"] < 5).astype(int)
    cc_df["moderateEdit"] = (
        (cc_df["contentChange"] >= 5)
        & (cc_df["contentChange"] < (0.5 * cc_df["contentTotal"]))
    ).astype(int)
    cc_df["majorEdit"] = (
        (cc_df["contentChange"] >= 5)
        & (cc_df["contentChange"] >= (0.5 * cc_df["contentTotal"]))
    ).astype(int)
    return cc_df


def build_notes_data(notes_df: pd.DataFrame) -> pd.DataFrame:
    notes_df = notes_df[
        [
            "referenceId",
            "date",
            "notesType.open",
            "notesType.resolved",
            "notesType.conflict",
        ]
    ]
    notes_df = notes_df.rename(
        columns={
            "referenceId": "book",
            "notesType.open": "openNotes",
            "notesType.resolved": "resolvedNotes",
            "notesType.conflict": "conflictNotes",
        }
    )
    notes_df["notesUpdated"] = 1
    return notes_df


def build_verse_count_df(verses_df: pd.DataFrame):
    verses_df["name"] = verses_df["book"]
    verses_df["verse"] = verses_df["referenceNumber"] % 1000
    verses_df["chapter"] = (verses_df["referenceNumber"] // 1000) % 1000
    verses_df["book"] = verses_df["referenceNumber"] // 1000000
    book_count_df = (
        verses_df[["name", "book", "verse"]]
        .groupby(["book"])
        .agg({"verse": "count", "name": "first"})
    )
    return book_count_df


def convert_to_int_if_possible(df):
    """
    Converts DataFrame columns to integer type if all values in the column are integers.

    Args:
        df (pd.DataFrame): Input DataFrame.

    Returns:
        pd.DataFrame: DataFrame with columns converted to integer type where applicable.
    """
    for col in df.columns:
        if pd.api.types.is_numeric_dtype(df[col]):
            if all(
                df[col] % 1 == 0
            ):  # Check if all values are integers or can be coerced to integers
                df[col] = pd.to_numeric(df[col], errors="raise", downcast="integer")
    return df


def build_book_data(
    cc_df: pd.DataFrame, notes_df: pd.DataFrame, revision_df: pd.DataFrame
):
    cc_and_notes_df = cc_df.merge(
        notes_df,
        on=["date", "book"],
        how="outer",
    )
    merged_df = cc_and_notes_df.merge(
        revision_df[["date"]],
        on="date",
        how="outer",
        sort="date",
    )

    notesColumns = ["openNotes", "resolvedNotes", "conflictNotes"]
    merged_df.loc[:, notesColumns] = merged_df.groupby("book")[notesColumns].ffill()

    merged_df.fillna(0, inplace=True)

    book_group = merged_df.groupby(["book", "date"])

    book_df = book_group.agg(
        {
            "contentTotal": "sum",
            "firstDraft": "sum",
            "minorEdit": "sum",
            "moderateEdit": "sum",
            "majorEdit": "sum",
            "contentChange": "sum",
            "openNotes": "max",
            "resolvedNotes": "max",
            "conflictNotes": "max",
        }
    )
    book_df = book_df.reset_index()
    book_df = convert_to_int_if_possible(book_df)

    book_df["dateTime"] = pd.to_datetime(book_df["date"].astype("Int64") * 1000000000)
    book_grouped = book_df.groupby("book")
    book_df["firstDraft_cumsum"] = book_grouped["firstDraft"].cumsum()
    book_df["minorEdit_cumsum"] = book_grouped["minorEdit"].cumsum()
    book_df["moderateEdit_cumsum"] = book_grouped["moderateEdit"].cumsum()
    book_df["majorEdit_cumsum"] = book_grouped["majorEdit"].cumsum()
    book_df["contentChange_cumsum"] = book_grouped["contentChange"].cumsum()
    book_df["openNotes"] = book_grouped["openNotes"].ffill()
    book_df["resolvedNotes"] = book_grouped["resolvedNotes"].ffill()
    book_df["conflictNotes"] = book_grouped["conflictNotes"].ffill()
    return book_df


def correct_time(book_df: pd.DataFrame):
    book_df["timeFromStart"] = book_df["dateTime"] - book_df["dateTime"].min()
    book_df["timeBetweenEdits"] = book_df["dateTime"].diff()
    book_df["timeBetweenEdits"] = book_df["timeBetweenEdits"].fillna(pd.Timedelta(0))
    # shorten any gaps over 3 weeks to be one week of time
    book_df["wasIdle"] = book_df["timeBetweenEdits"] > pd.Timedelta(21, unit="d")
    book_df["timeBetweenEditsRemoveIdle"] = book_df["timeBetweenEdits"]
    book_df.loc[book_df["wasIdle"], "timeBetweenEditsRemoveIdle"] = pd.Timedelta(
        7, unit="d"
    )
    book_df["timeFromStartRemoveIdle"] = book_df["timeBetweenEditsRemoveIdle"].cumsum()
    return book_df


# %%
