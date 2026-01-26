# %%
import json
import os
import pickle
from typing import Generator
import pytz
from tqdm import tqdm
from datetime import datetime

import numpy as np
import pandas as pd
import regex as re
from clearml.backend_api.session.client import APIClient
from clearml import Task
from matplotlib import pyplot as plt

clearml_stats_path = os.path.join(os.path.dirname(__file__), "clearml_stats")
project_pickle_filename = os.path.join(clearml_stats_path, "projects.pkl")
tasks_pickle_filename = os.path.join(clearml_stats_path, "tasks.pkl")
language_database_filename = os.path.join(clearml_stats_path, "Language.xlsx")
language_project_output_filename = os.path.join(
    clearml_stats_path, "language_projects.csv"
)
os.makedirs(clearml_stats_path, exist_ok=True)

re_lang_code = re.compile(
    r"((?<=-)\p{Ll}{3}(?=_)|(?<=[/\\_ ])\p{Ll}{3}(?=[-_])|(?<=-)\p{Ll}{3}(?=\.)|(?<=\.)\p{Ll}{3}(?=-)|(?<=FT-)[\p{Lu}\p{Ll}]{3}(?=[-_/\\]))"
)
re_lang_name = re.compile(r"\p{Lu}[a-z][\p{Lu}\p{Ll}]+")

langtags_url = (
    "https://raw.githubusercontent.com/silnrsi/langtags/master/source/langtags.csv"
)
langtags_df = pd.read_csv(langtags_url, index_col=0)

PAGE_SIZE = 500


def normalize_name(language_name: str) -> str:
    # break apart on capitalization or spaces, rearrange parts alphabetically
    return "".join(sorted(re.findall(r"\p{Lu}\p{Ll}*", language_name)))


def short_name(language_name: str) -> str:
    # get the first capitalized word and return it
    return re.findall(r"\p{Lu}\p{Ll}*", language_name)[0]


# %%
class clearml_stats:
    def __init__(self, refresh=False):
        self._client: APIClient = APIClient()
        self._tasks: dict[str, dict] = self._read_tasks()
        self._projects: dict[str, dict] = self._read_projects()
        self._languages: pd.DataFrame = pd.read_excel(
            language_database_filename, index_col=0
        )
        self._lang_name_to_code = self._create_language_name_to_code()
        self._project_id_to_task_id: dict[str, list[str]] = {}
        if len(self._tasks) == 0 or len(self._projects) == 0 or refresh:
            if refresh:
                self._tasks = {}
                self._projects = {}
            self.update_tasks_and_projects()
            self._tasks: dict[str, dict] = self._read_tasks()
            self._projects: dict[str, dict] = self._read_projects()
        for task_id in self._tasks.keys():
            project_id = self._tasks[task_id]["project"]
            if project_id in self._project_id_to_task_id:
                self._project_id_to_task_id[project_id].append(task_id)
            else:
                self._project_id_to_task_id[project_id] = [task_id]

    def update_tasks_and_projects(self):
        last_update = self._last_update()
        projects = self._get_projects(last_update)
        tasks = self._get_tasks(last_update)
        self._update_tasks(tasks)
        self._update_projects(projects)

    def get_tasks_by_project(self, project_name_filter: str = "") -> pd.DataFrame:
        if project_name_filter == "":
            return self._get_tasks(self._tasks.keys())
        else:
            projects_chosen = [
                project
                for project in self._projects
                if project_name_filter in self._projects[project]["name"]
            ]
            tasks_chosen = [
                task_id
                for task_id in self._tasks.keys()
                if self._tasks[task_id]["project"] in projects_chosen
            ]
            return self._get_tasks(tasks_chosen)

    def get_tasks_by_queue_names(self, queue_names: list[str]) -> pd.DataFrame:
        queue_ids = self._client.queues.get_all()
        id_to_name = {queue.id: queue.name for queue in queue_ids}
        queue_ids = [queue.id for queue in queue_ids if queue.name in queue_names]
        tasks_chosen = [
            task_id
            for task_id in self._tasks.keys()
            if "queue" in self._tasks[task_id]["execution"]
            and self._tasks[task_id]["execution"]["queue"] in queue_ids
        ]
        task_df = self._get_tasks(tasks_chosen)
        task_df.loc[:, "queue"] = task_df["execution"].apply(
            lambda x: id_to_name[x["queue"]] if "queue" in x else "unknown"
        )
        return task_df

    def get_language_groups(self) -> pd.DataFrame:
        return self._language_projects_df.copy()

    def _get_tasks(self, task_ids: list[str]) -> pd.DataFrame:
        tasks = {task_id: self._tasks[task_id] for task_id in task_ids}
        tasks_df = pd.DataFrame.from_dict(tasks).T
        tasks_df.loc[:, "total_run_time"] = pd.to_timedelta(
            tasks_df["completed"] - tasks_df["started"]
        ).dt.total_seconds()
        return tasks_df

    def _last_update(self) -> datetime:
        if len(self._tasks) == 0:
            return datetime.fromtimestamp(1640998800).astimezone(
                pytz.UTC
            )  # Jan 1 2022 at 1 am - i.e., before FT drafting was released
        return pd.to_datetime(max([v["created"] for v in self._tasks.values()]))

    def _get_projects(self, last_update: datetime) -> Generator:
        page = 0
        while True:
            new_projects = self._client.projects.get_all(
                page=page, page_size=PAGE_SIZE, order_by=["-created"]
            )
            if len(new_projects) == 0:
                return
            page += 1
            time_of_last_project = pd.to_datetime(new_projects[-1].data.created)
            print(
                f"Update project list-> Time: {time_of_last_project}, current page: {page}, total projects so far: {page*PAGE_SIZE}"
            )
            for project in new_projects:
                yield project
            if last_update > time_of_last_project:
                return

    def _get_tasks(self, last_update: datetime) -> Generator[Task, None, None]:
        page = 0
        while True:
            new_tasks = self._client.tasks.get_all(
                page=page, page_size=PAGE_SIZE, order_by=["-created"]
            )
            if len(new_tasks) == 0:
                return
            page += 1
            time_of_last_task = pd.to_datetime(new_tasks[-1].data.created)
            for task in new_tasks:
                yield task
            if last_update > time_of_last_task:
                break

    def _update_tasks(self, new_task_generator: Generator[Task, None, None]):
        tasks = self._tasks
        new_tasks = (task for task in new_task_generator if task.id not in tasks)
        task_fields = set(
            [
                "script",
                "script_args",
                "hyperparams",
                "project",
                "execution",
                "id",
                "started",
                "completed",
            ]
        )
        for task in tqdm(new_tasks):
            data = {k: v for k, v in task.data.to_dict().items() if k in task_fields}
            try:
                script = data["script"]["diff"][:-11]
                args = script.split("args = ")
                data["script_args"] = json.loads(
                    args[1]
                    .replace("'", '"')
                    .replace('"""', "")
                    .replace("True", "true")
                    .replace("False", "false")
                )
            except:
                data["script_args"] = {}
            data["tags"] = task.tags
            tasks[task.id] = data
        self._save_tasks(tasks)

    def _update_projects(self, new_projects_generator: Generator):
        projects = self._projects
        new_projects = (
            project for project in new_projects_generator if project.id not in projects
        )
        for project in new_projects:
            projects[project.id] = {}
            projects[project.id]["name"] = project.name
            projects[project.id]["first_run"] = None
            projects[project.id]["last_run"] = None
            projects[project.id]["tasks"] = []
            projects[project.id]["tags"] = []
        for id, task in self._tasks.items():
            if task["project"] not in projects:
                projects[task["project"]] = {}
                projects[task["project"]]["name"] = "unknown"
                projects[task["project"]]["first_run"] = None
                projects[task["project"]]["last_run"] = None
                projects[task["project"]]["tasks"] = []
                projects[task["project"]]["tags"] = (
                    task["tags"] if "tags" in task else []
                )
            if id in projects[task["project"]]["tasks"]:
                continue
            if "started" in task:
                projects[task["project"]]["first_run"] = (
                    min(projects[task["project"]]["first_run"], task["started"])
                    if projects[task["project"]]["first_run"]
                    else task["started"]
                )
                projects[task["project"]]["last_run"] = (
                    max(projects[task["project"]]["last_run"], task["started"])
                    if projects[task["project"]]["last_run"]
                    else task["started"]
                )

            projects[task["project"]]["tasks"].append(task["id"])

        self._assign_project_type()
        self._assign_languages()
        self.create_language_projects()
        self._save_projects(projects)

    def create_language_projects(self):
        for project_id in self._projects:
            project = self._projects[project_id]
            if (
                project["minority_language_code"] not in self._languages.index
                or project["minority_language_code"] == "unknown"
            ):
                project["language_name"] = "unknown"
                project["country"] = "unknown"
                project["continent"] = "unknown"
                project["language_users"] = np.nan
                project["latitude"] = np.nan
                project["longitude"] = np.nan
                project["language_status"] = "unknown"
            else:
                lang = self._languages.loc[project["minority_language_code"]]
                project["language_name"] = lang["UnitName"]
                project["country"] = lang["PrimaryCountryName"]
                project["continent"] = lang["PrimaryContinent"]
                project["language_users"] = lang["FirstLanguageUsers"]
                project["latitude"] = lang["Latitude"]
                project["longitude"] = lang["Longitude"]
                project["language_status"] = lang["LanguageEGIDSName"]
            self._projects[project_id] = project
        langs_df = pd.DataFrame.from_dict(self._projects).T
        self._language_projects_df = langs_df.groupby(
            ["type", "minority_language_code", "name"]
        ).agg(
            {
                "src_lang": "sum",
                "trg_lang": "sum",
                "language_name": "first",
                "country": "first",
                "continent": "first",
                "language_users": "first",
                "latitude": "first",
                "longitude": "first",
                "language_status": "first",
                "first_run": "min",
                "last_run": "max",
                "tasks": "sum",
                "tags": "first",
            }
        )
        self._language_projects_df["num_tasks"] = self._language_projects_df[
            "tasks"
        ].apply(len)

        def get_from_langtags(field: str):
            return [
                langtags_df.loc[lang, field] if lang in langtags_df.index else "unknown"
                for _, lang, _ in self._language_projects_df.index
            ]

        self._language_projects_df["LangTags_subtag"] = get_from_langtags(
            "likely_subtag"
        )
        self._language_projects_df["LangTags_LangName"] = get_from_langtags("LangName")
        self._language_projects_df["LangTags_regions"] = get_from_langtags("regions")
        self._language_projects_df["LangTags_script"] = self._language_projects_df[
            "LangTags_subtag"
        ].str.extract("-([A-Za-z]*)")
        self._language_projects_df.to_csv(
            language_project_output_filename, date_format="%Y/%m/%d"
        )

    def _assign_project_type(self):
        for project_id in self._projects:
            project_name = self._projects[project_id]["name"]
            if "Machine/prod.serval-api.org" in project_name:
                self._projects[project_id]["type"] = "production"
            elif "Machine/qa-int.serval-api.org" in project_name:
                self._projects[project_id]["type"] = "internal_qa"
            elif "Machine/qa.serval-api.org" in project_name:
                self._projects[project_id]["type"] = "external_qa"
            elif "docker-compose" in project_name:
                self._projects[project_id]["type"] = "development"
            else:
                self._projects[project_id]["type"] = "research"

    def _assign_languages(self):
        langs_by_occurrence = {"unknown": 0}

        def add_lang(lang):
            if lang in langs_by_occurrence:
                langs_by_occurrence[lang] += 1
            else:
                langs_by_occurrence[lang] = 1

        for project_id in self._projects:
            self._projects[project_id]["src_lang"] = "unknown"
            self._projects[project_id]["trg_lang"] = "unknown"
            self._projects[project_id]["lang_candidates"] = []

            project = self._projects[project_id]

            if project_id in self._project_id_to_task_id:
                project["tasks"] = self._project_id_to_task_id[project_id]
                task = self._tasks[project["tasks"][0]]
                args = task["script_args"]
                if "src_lang" in args and "trg_lang" in args:
                    self._projects[project_id]["src_lang"] = args["src_lang"].split(
                        "_"
                    )[0]
                    self._projects[project_id]["trg_lang"] = args["trg_lang"].split(
                        "_"
                    )[0]
                    add_lang(self._projects[project_id]["src_lang"])
                    add_lang(self._projects[project_id]["trg_lang"])
                else:
                    command = task["script"]["entry_point"]
                    if len(command.split(" ")) < 3:
                        continue
                    if "Demo" in command:
                        continue
                    # exclude python file from command
                    command = " ".join(command.split(" ")[2:])
                    lang_code_candidates = np.unique(
                        [
                            candidate.lower()
                            for candidate in re_lang_code.findall(command)
                            if candidate.lower() in self._languages.index
                        ]
                        + [
                            self._lang_name_to_code[normalize_name(lang_name)]
                            for lang_name in re_lang_name.findall(command)
                            if normalize_name(lang_name) in self._lang_name_to_code
                        ]
                    )
                    if len(lang_code_candidates) == 2:
                        self._projects[project_id]["src_lang"] = lang_code_candidates[0]
                        self._projects[project_id]["trg_lang"] = lang_code_candidates[1]
                        add_lang(self._projects[project_id]["src_lang"])
                        add_lang(self._projects[project_id]["trg_lang"])
                    elif len(lang_code_candidates) == 1:
                        self._projects[project_id]["trg_lang"] = lang_code_candidates[0]
                        add_lang(self._projects[project_id]["trg_lang"])
                    if len(lang_code_candidates) == 0:
                        print(
                            f"Project {project_id} has no language candidates: "
                            + command
                        )
                if self._projects[project_id]["src_lang"] == "unknown":
                    try:
                        config = json.loads(
                            task["hyperparams"]["config"]["data/corpus_pairs"][
                                "value"
                            ].replace("'", '"')
                        )[0]
                        if isinstance(config["src"], list):
                            self._projects[project_id]["src_lang"] = (
                                config["src"][0].split("-")[0]
                                if isinstance(config["src"][0], str)
                                else config["src"][0]["name"].split("-")[0]
                            )

                        else:
                            self._projects[project_id]["src_lang"] = config[
                                "src"
                            ].split("-")[0]

                        if len(self._projects[project_id]["src_lang"]) == 2:
                            two_letter_tag = self._projects[project_id]["src_lang"]
                            self._projects[project_id]["src_lang"] = task[
                                "hyperparams"
                            ]["config"][f"data/lang_codes/{two_letter_tag}"][
                                "value"
                            ].split(
                                "_"
                            )[
                                0
                            ]
                        if isinstance(config["trg"], list):
                            self._projects[project_id]["trg_lang"] = (
                                config["trg"][0].split("-")[0]
                                if isinstance(config["trg"][0], str)
                                else config["trg"][0]["name"].split("-")[0]
                            )
                        else:
                            self._projects[project_id]["trg_lang"] = config[
                                "trg"
                            ].split("-")[0]
                        if len(self._projects[project_id]["trg_lang"]) == 2:
                            two_letter_tag = self._projects[project_id]["trg_lang"]
                            self._projects[project_id]["trg_lang"] = task[
                                "hyperparams"
                            ]["config"][f"data/lang_codes/{two_letter_tag}"][
                                "value"
                            ].split(
                                "_"
                            )[
                                0
                            ]
                        add_lang(self._projects[project_id]["src_lang"])
                        add_lang(self._projects[project_id]["trg_lang"])
                    except KeyError:
                        pass
                    except Exception as e:
                        print(task)
                        raise e

        for project_id in self._projects:
            project = self._projects[project_id]
            if (
                project["src_lang"] not in langs_by_occurrence
                or project["trg_lang"] not in langs_by_occurrence
                or langs_by_occurrence[project["src_lang"]]
                > langs_by_occurrence[project["trg_lang"]]
            ) and not "BT" in project["name"]:
                project["minority_language_code"] = project["trg_lang"].lower()
            else:
                project["minority_language_code"] = project["src_lang"].lower()

    def _create_language_name_to_code(self):
        # name parts lowest priority
        lang_name_to_code = {
            lang_part: index
            for index, lang in self._languages.iterrows()
            for lang_part in lang["UnitName"].replace("-", " ").split()
        }
        # Then the first part of a name
        lang_name_to_code.update(
            {
                short_name(lang["UnitName"]): index
                for index, lang in self._languages.iterrows()
            }
        )
        # then (hightest priority), the full, normalized name
        lang_name_to_code.update(
            {
                normalize_name(lang["UnitName"]): index
                for index, lang in self._languages.iterrows()
            }
        )
        return lang_name_to_code

    def _save_projects(self, projects):
        self._projects = projects
        with open(project_pickle_filename, "wb") as f:
            pickle.dump(projects, f)

    def _save_tasks(self, tasks):
        self._tasks = tasks
        with open(tasks_pickle_filename, "wb") as f:
            pickle.dump(tasks, f)

    def _read_projects(self):
        if os.path.exists(project_pickle_filename):
            with open(project_pickle_filename, "rb") as f:
                return pickle.load(f)
        return {}

    def _read_tasks(self):
        if os.path.exists(tasks_pickle_filename):
            with open(tasks_pickle_filename, "rb") as f:
                return pickle.load(f)
        return {}


def plot_loading_per_week(tasks_df: pd.DataFrame, title_prefix="", group_by="status"):
    tasks_df["week"] = pd.to_datetime(tasks_df.started).dt.strftime("%Y-%U")
    seconds_per_week = 60 * 60 * 24 * 7
    by_week = pd.crosstab(
        index=tasks_df.week,
        columns=tasks_df[group_by],
        values=tasks_df.total_run_time / seconds_per_week,
        aggfunc="sum",
    )
    by_week.plot(
        kind="bar",
        stacked=True,
        title=title_prefix + ": agent loading per week",
        grid=True,
    )


def violin_task_run_time_per_week(
    tasks_df: pd.DataFrame, title_prefix="", num_of_weeks=10, short_run_time: int = 600
):
    fig, axes = plt.subplots()
    by_week = {
        week: tasks_df[tasks_df["week"] == week]["total_run_time"].to_numpy()
        for week in tasks_df["week"].unique()
    }
    # filter out things under 10 minutes
    by_week = {
        week: times[times > short_run_time] / 60 / 60
        for week, times in by_week.items()
        if len(times[times > short_run_time]) > 1
    }
    # last x weeks only
    by_week = {
        week: by_week[week] for week in np.sort(list(by_week.keys()))[-num_of_weeks:]
    }
    axes.violinplot(dataset=list(by_week.values()), showmedians=True)
    axes.set_title(title_prefix + ": Task run time for last 10 weeks")
    axes.set_xticklabels(by_week.keys(), rotation=45)
    axes.set_xticks(range(1, len(by_week) + 1))
    axes.set_ylabel("hours")
    axes.set_ylim(0, 16)
    axes.grid(True)


def violin_task_delay_time_per_week(
    tasks_df: pd.DataFrame, title_prefix="", num_of_week=10
):
    tasks_df.loc[:, "delay_time"] = pd.to_timedelta(
        tasks_df["started"] - tasks_df["created"]
    ).dt.total_seconds()
    fig, axes = plt.subplots()
    by_week = {
        week: tasks_df[tasks_df["week"] == week]["delay_time"].to_numpy()
        for week in tasks_df["week"].unique()
    }
    # filter out things under 10 minutes
    by_week = {
        week: times / 60 / 60 for week, times in by_week.items() if len(times) > 1
    }
    # last 10 weeks only
    by_week = {
        week: by_week[week] for week in np.sort(list(by_week.keys()))[-num_of_week:]
    }
    axes.violinplot(dataset=list(by_week.values()), showmeans=True)
    axes.set_title(title_prefix + ": Task delay time for last 10 weeks")
    axes.set_xticklabels(by_week.keys(), rotation=45)
    axes.set_xticks(range(1, len(by_week) + 1))
    axes.set_ylim(0, 8)
    axes.set_ylabel("hours")
    axes.grid(True)


# %%
