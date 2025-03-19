# %%
import json
import os

import pandas as pd


class ParatextProjectByWeek:
    def __init__(self, root_folder: str):
        self.process_path = os.path.join(root_folder, "local/stats")
        self.meta_df = None
        self.week_df = None
        self._book_count_df = None

    def process(self):

        self._load_meta_data()

        book_df = self._build_book_data()
        week_df = self._to_weeks(book_df)
        week_df = self._calculate_idle_time(week_df)
        week_df = self._add_verse_data(week_df)
        self.week_df = week_df

    def save_week_data(self):
        if self.week_df is None:
            self.process()
        self.week_df.to_csv(
            os.path.join(self.process_path, "week_data.csv"),
            index=False,
        )

    def _load_meta_data(self) -> pd.DataFrame:
        self.meta_df = json.load(open(os.path.join(self.process_path, "meta.json")))

    def _load_revision_data(
        self,
    ) -> tuple[pd.DataFrame, pd.DataFrame, pd.DataFrame]:

        def load_json(file_name: str) -> str:
            return json.load(
                open(
                    os.path.join(
                        self.process_path,
                        file_name,
                    )
                )
            )

        revision_df = pd.DataFrame(load_json("revisionStatus.json")).T
        cc_df = pd.DataFrame(load_json("characterCount.json"))
        cc_df = self._build_character_data(cc_df)
        notes = load_json("openNotesCount.json")
        notes_df = pd.json_normalize(notes, record_path="byBook", meta=["date"])
        notes_df = self._build_notes_data(notes_df)

        return revision_df, cc_df, notes_df

    def _build_character_data(self, cc_df: pd.DataFrame) -> pd.DataFrame:
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

    def _build_notes_data(self, notes_df: pd.DataFrame) -> pd.DataFrame:
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

    def _build_book_data(self) -> pd.DataFrame:
        revision_df, cc_df, notes_df = self._load_revision_data()

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
        book_df = _convert_to_int_if_possible(book_df)

        book_df["dateTime"] = pd.to_datetime(
            book_df["date"].astype("Int64") * 1000000000
        )
        book_grouped = book_df.groupby("book")
        book_df["openNotes"] = book_grouped["openNotes"].ffill()
        book_df["resolvedNotes"] = book_grouped["resolvedNotes"].ffill()
        book_df["conflictNotes"] = book_grouped["conflictNotes"].ffill()
        return book_df

    def _to_weeks(self, book_df: pd.DataFrame) -> pd.DataFrame:
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

    def _calculate_idle_time(self, week_df: pd.DataFrame) -> pd.DataFrame:
        weeksBetweenEdits = week_df["weeksFromStart"].diff()
        weeksBetweenEdits = weeksBetweenEdits.fillna(0)
        # shorten any gaps over 3 weeks to be one week of time
        week_df["wasIdle"] = weeksBetweenEdits >= 3
        weeksBetweenEdits[week_df["wasIdle"]] = 1
        week_df["weeksFromStartRemoveIdle"] = weeksBetweenEdits.cumsum().astype(int)
        return week_df

    def _add_verse_data(self, week_df: pd.DataFrame) -> pd.DataFrame:
        self._load_verse_count_df()
        return week_df.merge(self._book_count_df, on="book", how="left")

    def _load_verse_count_df(self):
        if self._book_count_df is not None:
            return
        verses_df = pd.DataFrame(
            json.load(open(os.path.join(self.process_path, "verses.json")))
        )
        verses_df["name"] = verses_df["book"]
        verses_df["verse"] = verses_df["referenceNumber"] % 1000
        verses_df["chapter"] = (verses_df["referenceNumber"] // 1000) % 1000
        verses_df["book"] = verses_df["referenceNumber"] // 1000000
        book_count_df = (
            verses_df[["name", "book", "verse"]]
            .groupby(["book"])
            .agg({"verse": "count", "name": "first"})
        )
        # append a "book 0" called NONE
        book_count_df = pd.concat(
            [pd.DataFrame({"verse": [0], "name": ["NONE"]}, index=[0]), book_count_df]
        )
        book_count_df.sort_index(inplace=True)
        book_count_df["book"] = book_count_df.index
        book_count_df = book_count_df.rename(columns={"verse": "verseCount"})
        self._book_count_df = book_count_df


def _convert_to_int_if_possible(df):
    for col in df.columns:
        if pd.api.types.is_numeric_dtype(df[col]):
            if all(
                df[col] % 1 == 0
            ):  # Check if all values are integers or can be coerced to integers
                df[col] = pd.to_numeric(df[col], errors="raise", downcast="integer")
    return df


# %%
