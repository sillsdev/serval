# %%
import clearml_stats
import numpy as np
import pandas as pd
from clearml.backend_api.session.client import APIClient
from matplotlib import pyplot as plt

client = APIClient()


# %%
days_since_last_run = 2

project_list = clearml_stats.get_project_list(client)
project_names = [project.name for project in project_list]
task_list = clearml_stats.get_task_list(client, days=days_since_last_run)
tasks = clearml_stats.get_tasks(task_list)
projects = clearml_stats.get_projects(project_list, tasks)

# %%
prod_name = "Machine/prod.serval-api.org"
# prod_name = "Machine"
projects_chosen = [
    project for project in projects if prod_name in projects[project]["name"]
]
tasks_chosen = [
    tasks[task] for task in tasks if tasks[task]["project"] in projects_chosen
]
# tasks_chosen = [tasks[task] for task in tasks]
# %%

# graph the tasks started per day using pandas
tasks_df = pd.DataFrame.from_dict(tasks_chosen)
tasks_df.loc[:, "total_run_time"] = pd.to_timedelta(
    tasks_df["completed"] - tasks_df["started"]
).dt.total_seconds()
mask = tasks_df["total_run_time"] > 600
# mask = tasks_df["total_run_time"] > -1
tasks_df["week"] = pd.to_datetime(tasks_df["started"]).dt.strftime("%Y-%U")
by_week = pd.crosstab(
    index=tasks_df.loc[mask, "week"], columns=tasks_df.loc[mask, "status"]
)
by_week.plot(kind="bar", stacked=True, title="Tasks started per week", grid=True)
# %%
tasks_df.loc[:, "total_run_time"] = pd.to_timedelta(
    tasks_df["completed"] - tasks_df["started"]
).dt.total_seconds()
tasks_df.hist(column="total_run_time", bins=100)

# %%
fig, axes = plt.subplots()
by_week = {
    week: tasks_df[tasks_df["week"] == week]["total_run_time"].to_numpy()
    for week in tasks_df["week"].unique()
}
# filter out things under 10 minutes
by_week = {
    week: times[times > 600] / 60 / 60
    for week, times in by_week.items()
    if len(times[times > 600]) > 1
}
# last 10 weeks only
by_week = {week: by_week[week] for week in np.sort(list(by_week.keys()))[-10:]}
axes.violinplot(dataset=list(by_week.values()), showmedians=True)
axes.set_title("Task run time for last 10 weeks")
axes.set_xticklabels(by_week.keys(), rotation=45)
axes.set_xticks(range(1, len(by_week) + 1))
axes.set_ylabel("hours")
axes.set_ylim(0, 16)
axes.grid(True)

# %%
tasks_df.loc[:, "delay_time"] = pd.to_timedelta(
    tasks_df["started"] - tasks_df["created"]
).dt.total_seconds()
fig, axes = plt.subplots()
by_week = {
    week: tasks_df[tasks_df["week"] == week]["delay_time"].to_numpy()
    for week in tasks_df["week"].unique()
}
# filter out things under 10 minutes
by_week = {week: times / 60 / 60 for week, times in by_week.items() if len(times) > 1}
# last 10 weeks only
by_week = {week: by_week[week] for week in np.sort(list(by_week.keys()))[-10:]}
axes.violinplot(dataset=list(by_week.values()), showmeans=True)
axes.set_title("Task delay time for last 10 weeks")
axes.set_xticklabels(by_week.keys(), rotation=45)
axes.set_xticks(range(1, len(by_week) + 1))
axes.set_ylim(0, 8)
axes.set_ylabel("hours")
axes.grid(True)

# %%
