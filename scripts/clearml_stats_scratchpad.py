# %%
import clearml_stats
import plotly.express as px

# %%
stats = clearml_stats.clearml_stats()
stats.update_tasks_and_projects()
lang_groups_df = stats.get_language_groups()


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
