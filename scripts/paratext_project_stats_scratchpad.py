# %%
import os

import clearml_stats
from pp_stats.paratext_project_plotter import plot_combined_progress
from pp_stats.paratext_project_processor import ParatextProjectProcessor

# %%
stats = clearml_stats.clearml_stats()
stats.update_tasks_and_projects()
stats.create_language_projects()
# %%
pp_folder = os.environ["PARATEXT_PROJECT_STATS_FOLDER"]
ppp = ParatextProjectProcessor(pp_folder)
ppp.process()
for pp_id, pp in ppp.week_data.items():
    language_name = pp.meta_dict["language"]
    language_code = pp.meta_dict["languageCode"].split(":")[0]
    if language_name in stats.lang_name_to_code:
        language_code = stats.lang_name_to_code[language_name]
    if language_code in stats._language_projects_df.index:
        language = stats._language_projects_df.loc[language_code, :]
        tasks = language["tasks"]
        completion_times = [
            stats._tasks[task]["completed"]
            for task in tasks
            if "completed" in stats._tasks[task]
        ]
    else:
        print(f"Language {language_code} not found in language projects.")
        completion_times = []

    combined_df = pp.get_combined_progress(False)
    fig = plot_combined_progress(
        combined_df,
        draft_events=completion_times,
        title=f"{pp_id.split('_')[0]}: {language_name} - {len(completion_times)} tasks",
        year_start=2023,
    )
    fig.write_image(os.path.join(pp_folder, f"combined_progress_{pp_id}.png"))
    fig = plot_combined_progress(
        combined_df,
        draft_events=completion_times,
        title=f"{pp_id.split('_')[0]}: {language_name} - {len(completion_times)} tasks",
    )
    fig.write_html(os.path.join(pp_folder, f"combined_progress_{pp_id}.html"))
    pp.save_week_data()
    print(f"Processed project {pp_id} with language {language_name}: {language_code}.")
# %%
