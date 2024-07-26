# %%
import clearml_stats
import numpy as np
import pandas as pd
from clearml.backend_api.session.client import APIClient
from matplotlib import pyplot as plt

client = APIClient()


# %%
days_since_last_run = 1

project_list = clearml_stats.get_project_list(client)
project_names = [project.name for project in project_list]
task_list = clearml_stats.get_task_list(client, days=days_since_last_run)
tasks = clearml_stats.get_tasks(task_list)
projects = clearml_stats.get_projects(project_list, tasks)

# %%
prod_name = "Machine/prod.serval-api.org"
# prod_name = "Machine"
# projects_chosen = [
#    project for project in projects if prod_name in projects[project]["name"]
# ]
# tasks_chosen = {
#    task: tasks[task] for task in tasks if tasks[task]["project"] in projects_chosen
# }

# choose all
projects_chosen = projects
tasks_chosen = [tasks[task] for task in tasks]

tasks_df = pd.DataFrame.from_dict(tasks_chosen).T
tasks_df.loc[:, "total_run_time"] = pd.to_timedelta(
    tasks_df["completed"] - tasks_df["started"]
).dt.total_seconds()

# %%
clearml_stats.plot_tasks_per_week(tasks_df)
clearml_stats.violin_task_run_time_per_week(tasks_df)
clearml_stats.violin_task_delay_time_per_week(tasks_df)

# %%
langs_by_occurence = {}


def add_lang(lang):
    if lang in langs_by_occurence:
        langs_by_occurence[lang] += 1
    else:
        langs_by_occurence[lang] = 1


num_of_tasks_found = 0
num_of_tasks_not_found = 0
for project_id in projects:
    project = projects[project_id]
    if len(project["tasks"]) > 0:
        task_not_found = True
        for task_id in project["tasks"]:
            if task_id in tasks_df.index:
                task_not_found = False
                break
        if task_not_found:
            num_of_tasks_not_found += 1
            continue
        num_of_tasks_found += 1
        task = tasks_df.loc[project["tasks"][0]]
        args = task.script_args
        add_lang(args["src_lang"].split("_")[0])
        add_lang(args["trg_lang"].split("_")[0])

# %%
