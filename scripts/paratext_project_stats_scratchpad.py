# %%
import os

import clearml_stats
import pandas as pd
from paratext_project_processor import ParatextProjectProcessor
from plotly import express as px

pd.options.plotting.backend = "plotly"

# %%
stats = clearml_stats.clearml_stats()
# stats.update_tasks_and_projects()
# stats.create_language_projects()

# %%
pp_folder = os.environ["PARATEXT_PROJECT_STATS_FOLDER"]
ppp = ParatextProjectProcessor(pp_folder)
ppp.process()


# %%
week_df = ppp.week_data["ESWE8_c081cd88e02904fdf4ef621358a900d37d89e2b5"].week_df.copy()
week_df["percentDrafted"] = week_df["firstDraft_cumsum"] / week_df["verseCount"] * 100
week_df.set_index("weeksFromStartRemoveIdle", inplace=True)
grouped = week_df.groupby("book")["percentDrafted"].reset_index()
fig = px.line(grouped, x="book", y="percentDrafted", title="Percent Drafted by Book")
fig.show()
# %%
