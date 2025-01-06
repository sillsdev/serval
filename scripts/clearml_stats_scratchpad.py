# %%
import clearml_stats
import numpy as np
import plotly.express as px

# %%
stats = clearml_stats.clearml_stats()
stats.update_tasks_and_projects()
stats.create_language_projects()


# %%
def plot_by_queue(queue_names: list[str], prefix: str = ""):
    tasks_df = stats.get_tasks_by_queue_names(queue_names)
    if prefix == "":
        prefix = ", ".join(queue_names)
    if len(queue_names) > 1:
        clearml_stats.plot_loading_per_week(
            tasks_df, group_by="queue", title_prefix=prefix
        )
    else:
        clearml_stats.plot_loading_per_week(
            tasks_df, group_by="status", title_prefix=prefix
        )
    clearml_stats.violin_task_run_time_per_week(tasks_df, title_prefix=prefix)
    clearml_stats.violin_task_delay_time_per_week(tasks_df, title_prefix=prefix)


# %%
# plot_by_queue(["production"])
# plot_by_queue(["jobs_urgent"])
# plot_by_queue(["jobs_backlog"])
plot_by_queue(["production", "jobs_urgent", "jobs_backlog"], prefix="A100")

# %%
lang_groups_df = stats.get_language_groups()
lang_groups_df["num_tasks"] = lang_groups_df["tasks"].apply(len)

mask = ~(lang_groups_df["language_name"] == "unknown") & (
    lang_groups_df["type"] == "production"
)
fig = px.scatter(
    lang_groups_df[mask],
    x="first_run",
    y="last_run",
    color="continent",
    hover_name="language_name",
)

counts = lang_groups_df[mask]["continent"].value_counts()

# Update legend labels
for i, trace in enumerate(fig.data):
    category = trace.name
    count = counts[category]
    fig.data[i].name = f"{category} ({count})"

fig.show()
# %%
lang_groups_df = stats.get_language_groups()
mask = ~(lang_groups_df["language_name"] == "unknown")
lang_groups_df.loc[
    (lang_groups_df["last_run"] - lang_groups_df["first_run"]) < np.timedelta64(1, "W"),
    "type",
] = "stale"

fig = px.scatter(
    lang_groups_df[mask],
    x="first_run",
    y="last_run",
    color="type",
    hover_name="language_name",
)


# Calculate counts for each type
counts = lang_groups_df[mask]["type"].value_counts()

# Update legend labels
for i, trace in enumerate(fig.data):
    category = trace.name
    count = counts[category]
    fig.data[i].name = f"{category} ({count})"

fig.show()
