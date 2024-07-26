# %%
import json
import os
import pickle
from datetime import datetime, timezone

import numpy as np
import pandas as pd
import regex as re
from clearml import Task
from clearml.backend_api.session.client import APIClient
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


def normalize_name(language_name: str) -> str:
    # break apart on capatitalization or spaces, rearrange parts alphabetically
    return "".join(sorted(re.findall(r"\p{Lu}\p{Ll}*", language_name)))


def short_name(language_name: str) -> str:
    # get the first captialized word and return it
    return re.findall(r"\p{Lu}\p{Ll}*", language_name)[0]


# %%
class clearml_stats:

    def __init__(self):
        self._client: APIClient = APIClient()
        self._tasks: dict[str, dict] = self._read_tasks()
        self._projects: dict[str, dict] = self._read_projects()
        self._languages: pd.DataFrame = pd.read_excel(
            language_database_filename, index_col=0
        )
        self._lang_name_to_code = self._create_language_name_to_code()

    def update_tasks_and_projects(self):
        last_update = self._last_update()
        project_list = self._get_project_list(last_update)
        task_list = self._get_task_list(last_update)
        self._update_tasks(task_list)
        self._update_projects(project_list)
        return

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
        return self._language_projects_df

    def _get_tasks(self, task_ids: list[str]) -> pd.DataFrame:
        tasks = {task_id: self._tasks[task_id] for task_id in task_ids}
        tasks_df = pd.DataFrame.from_dict(tasks).T
        tasks_df.loc[:, "total_run_time"] = pd.to_timedelta(
            tasks_df["completed"] - tasks_df["started"]
        ).dt.total_seconds()
        return tasks_df

    def _last_update(self) -> datetime:
        return pd.to_datetime(max([v["created"] for v in self._tasks.values()]))

    def _get_project_list(self, last_update: datetime) -> list:
        project_list = []
        page = 0
        while True:
            new_projects = self._client.projects.get_all(
                page=page, page_size=25, order_by=["-created"]
            )
            if len(new_projects) == 0:
                break
            project_list += new_projects
            page += 1
            time_of_last_project = pd.to_datetime(
                self._client.projects.get_by_id(new_projects[-1].id).data.created
            )
            print(
                f"Update project list-> Time: {time_of_last_project}, current page: {page}"
            )
            if last_update > time_of_last_project:
                break
        return project_list

    def _get_task_list(self, last_update: datetime) -> list:
        task_list = []
        page = 0
        while True:
            new_tasks = self._client.tasks.get_all(
                page=page, page_size=25, order_by=["-created"]
            )
            if len(new_tasks) == 0:
                break
            task_list += new_tasks
            page += 1
            time_of_last_task = pd.to_datetime(
                Task.get_task(new_tasks[-1].id).data.created
            )
            print(
                f"Update tasks list-> Time: {time_of_last_task}, current page: {page}"
            )
            if last_update > time_of_last_task:
                break
        return task_list

    def _update_tasks(self, task_list):
        tasks = self._tasks
        new_tasks = [task for task in task_list if task.id not in tasks]
        num_of_tasks = len(new_tasks)
        cur_task_num = 0
        step = 10
        while cur_task_num < num_of_tasks:
            cur_tasks = Task.get_tasks(
                [t.id for t in new_tasks[cur_task_num : cur_task_num + step]]
            )
            if len(cur_tasks) == 0:
                print("what's wrong?")
                break
            for task in cur_tasks:
                data = task.data.to_dict()
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
                tasks[task.id] = data
            cur_task_num += step
            if cur_task_num % 50 == 0:
                print(f"Processed {cur_task_num} out of {num_of_tasks} tasks")
        self._save_tasks(tasks)

    def _update_projects(self, project_list: list):
        projects = self._projects
        new_projects = [
            project for project in project_list if project.id not in projects
        ]
        for project in new_projects:
            projects[project.id] = {}
            projects[project.id]["name"] = project.name
            projects[project.id]["first_run"] = None
            projects[project.id]["last_run"] = None
            projects[project.id]["tasks"] = []
        for id, task in self._tasks.items():
            if task["project"] not in projects:
                projects[task["project"]] = {}
                projects[task["project"]]["name"] = "unknown"
                projects[task["project"]]["first_run"] = None
                projects[task["project"]]["last_run"] = None
                projects[task["project"]]["tasks"] = []
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
        self._create_language_projects()
        self._save_projects(projects)

    def _create_language_projects(self):
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
        self._language_projects_df = langs_df.groupby("minority_language_code").agg(
            {
                "name": "first",
                "type": "last",
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
            }
        )
        self._language_projects_df["num_tasks"] = self._language_projects_df[
            "tasks"
        ].apply(len)

        def get_from_langtags(field: str):
            return [
                langtags_df.loc[lang, field] if lang in langtags_df.index else "unknown"
                for lang in self._language_projects_df.index
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
            elif "Machine/qa_int.serval-api.org" in project_name:
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

        num_of_tasks_found = 0
        num_of_tasks_not_found = 0
        for project_id in self._projects:
            self._projects[project_id]["src_lang"] = "unknown"
            self._projects[project_id]["trg_lang"] = "unknown"
            self._projects[project_id]["lang_candidates"] = []

            project = self._projects[project_id]
            if len(project["tasks"]) > 0:
                task_not_found = True
                for task_id in project["tasks"]:
                    if task_id in self._tasks.keys():
                        task_not_found = False
                        break
                if task_not_found:
                    num_of_tasks_not_found += 1
                    continue
                num_of_tasks_found += 1
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

        for project_id in self._projects:
            project = self._projects[project_id]
            if (
                langs_by_occurrence[project["src_lang"]]
                > langs_by_occurrence[project["trg_lang"]]
            ):
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
        with open(project_pickle_filename, "rb") as f:
            return pickle.load(f)

    def _read_tasks(self):
        with open(tasks_pickle_filename, "rb") as f:
            return pickle.load(f)


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
