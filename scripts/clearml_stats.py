# %%
import json
import os
import pickle
from datetime import datetime, timezone

import numpy as np
import pandas as pd
from clearml import Task
from clearml.backend_api.session.client import APIClient
from matplotlib import pyplot as plt


# %%
def get_project_list(client: APIClient):
    project_list = []
    page = 0
    while True:
        new_projects = client.projects.get_all(page=page)
        if len(new_projects) == 0:
            break
        project_list += new_projects
        page += 1
    return project_list


def get_task_list(client: APIClient, days=2):
    task_list = []
    page = 0
    now = datetime.now(timezone.utc)
    while True:
        new_tasks = client.tasks.get_all(
            page=page, page_size=100, order_by=["-created"]
        )
        if len(new_tasks) == 0:
            break
        task_list += new_tasks
        page += 1
        current_day = (
            now - pd.to_datetime(Task.get_task(new_tasks[-1].id).data.created)
        ).days
        print(f"Current day: {current_day}, current page: {page}")
        if current_day > days:
            break
    return task_list


def get_tasks(task_list):
    if os.path.exists("tasks.pkl"):
        with open("tasks.pkl", "rb") as f:
            tasks = pickle.load(f)
    else:
        tasks: dict[str, dict] = {}
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
    with open("tasks.pkl", "wb") as f:
        pickle.dump(tasks, f)
    return tasks


def get_projects(project_list, tasks):
    if os.path.exists("projects.pkl"):
        with open("projects.pkl", "rb") as f:
            projects = pickle.load(f)
    else:
        projects = {}
    new_projects = [project for project in project_list if project.id not in projects]
    for project in new_projects:
        projects[project.id] = {}
        projects[project.id]["name"] = project.name
        projects[project.id]["first_run"] = None
        projects[project.id]["last_run"] = None
        projects[project.id]["tasks"] = []
    for id, task in tasks.items():
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
    with open("projects.pkl", "wb") as f:
        pickle.dump(projects, f)
    return projects


def plot_tasks_per_week(tasks_df: pd.DataFrame, short_run_time: int = 600):
    mask = tasks_df["total_run_time"] > short_run_time
    tasks_df["week"] = pd.to_datetime(tasks_df["started"]).dt.strftime("%Y-%U")
    by_week = pd.crosstab(
        index=tasks_df.loc[mask, "week"], columns=tasks_df.loc[mask, "status"]
    )
    by_week.plot(kind="bar", stacked=True, title="Tasks started per week", grid=True)


def violin_task_run_time_per_week(
    tasks_df: pd.DataFrame, num_of_weeks=10, short_run_time: int = 600
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
    axes.set_title("Task run time for last 10 weeks")
    axes.set_xticklabels(by_week.keys(), rotation=45)
    axes.set_xticks(range(1, len(by_week) + 1))
    axes.set_ylabel("hours")
    axes.set_ylim(0, 16)
    axes.grid(True)


def violin_task_delay_time_per_week(tasks_df: pd.DataFrame, num_of_week=10):
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
    axes.set_title("Task delay time for last 10 weeks")
    axes.set_xticklabels(by_week.keys(), rotation=45)
    axes.set_xticks(range(1, len(by_week) + 1))
    axes.set_ylim(0, 8)
    axes.set_ylabel("hours")
    axes.grid(True)
