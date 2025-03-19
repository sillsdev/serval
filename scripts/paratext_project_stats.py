# %%
import json
import os

import numpy as np
import pandas as pd


def load_json_data(
    root_path: str,
) -> tuple[pd.DataFrame, pd.DataFrame, pd.DataFrame, pd.DataFrame]:
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
    book_df["openNotes"] = book_grouped["openNotes"].ffill()
    book_df["resolvedNotes"] = book_grouped["resolvedNotes"].ffill()
    book_df["conflictNotes"] = book_grouped["conflictNotes"].ffill()
    return book_df


def to_weeks(book_df: pd.DataFrame):
    start_date = book_df["dateTime"].min()
    one_week = pd.Timedelta(7, unit="d")
    book_df["weeksFromStart"] = (book_df["dateTime"] - start_date).dt.days // 7
    # create combined week and year column
    week_df = book_df.groupby(["book", "weeksFromStart"]).agg(
        {
            "contentTotal": "sum",
            "firstDraft": ["sum", "max"],
            "minorEdit": "sum",
            "moderateEdit": "sum",
            "majorEdit": "sum",
            "contentChange": "sum",
            "openNotes": "max",
            "resolvedNotes": "max",
            "conflictNotes": "max",
        }
    )
    week_df.sort_values(by=["weeksFromStart", "book"], inplace=True)
    week_df = week_df.reset_index()
    week_df.columns = [
        "_".join(col) if col[1] != "" else col[0] for col in week_df.columns
    ]
    week_df["dateTime"] = start_date + week_df["weeksFromStart"] * one_week
    week_df["year.week"] = week_df["dateTime"].dt.strftime("%Y.%U")

    week_grouped = week_df.groupby("book")
    week_df["firstDraft_cumsum"] = week_grouped["firstDraft_sum"].cumsum()
    week_df["minorEdit_cumsum"] = week_grouped["minorEdit_sum"].cumsum()
    week_df["moderateEdit_cumsum"] = week_grouped["moderateEdit_sum"].cumsum()
    week_df["majorEdit_cumsum"] = week_grouped["majorEdit_sum"].cumsum()
    week_df["contentChange_cumsum"] = week_grouped["contentChange_sum"].cumsum()
    return week_df


def calculate_idle_time(week_df: pd.DataFrame):
    weeksBetweenEdits = week_df["weeksFromStart"].diff()
    weeksBetweenEdits = weeksBetweenEdits.fillna(0)
    # shorten any gaps over 3 weeks to be one week of time
    week_df["wasIdle"] = weeksBetweenEdits >= 3
    weeksBetweenEdits[week_df["wasIdle"]] = 1
    week_df["weeksFromStartRemoveIdle"] = weeksBetweenEdits.cumsum()
    return week_df


# %%
