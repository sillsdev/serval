# %%
import clearml_stats
import numpy as np
import pandas as pd
import plotly.express as px
from clearml.backend_api.session.client import APIClient
from matplotlib import pyplot as plt

client = APIClient()


# %%
stats = clearml_stats.clearml_stats()
stats.update_tasks_and_projects()

# %%
prod_name = "Machine/prod.serval-api.org"
tasks_df = stats.get_tasks(prod_name)
lang_groups_df = stats.get_language_groups()
# %%
clearml_stats.plot_tasks_per_week(tasks_df)
clearml_stats.violin_task_run_time_per_week(tasks_df)
clearml_stats.violin_task_delay_time_per_week(tasks_df)

# %%
lang_groups_df = stats.get_language_groups()
lang_groups_df["num_tasks"] = lang_groups_df["tasks"].apply(len)

mask = ~(lang_groups_df["language_name"] == "unknown") & (
    lang_groups_df["type"] == "production"
)
px.scatter(
    lang_groups_df[mask],
    x="first_run",
    y="last_run",
    color="continent",
    hover_name="language_name",
)
# %%
mask = ~(lang_groups_df["language_name"] == "unknown") & (
    lang_groups_df["type"] == "research"
)
px.scatter(
    lang_groups_df[mask],
    x="first_run",
    y="last_run",
    color="continent",
    hover_name="language_name",
)
# %%
