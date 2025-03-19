# %%
import os

from paratext_project_by_week import ParatextProjectByWeek


class ParatextProjectProcessor:
    def __init__(self, parent_folder: str):
        self.parent_folder = parent_folder
        self.folders = os.listdir(parent_folder)
        self.week_data: dict[str, ParatextProjectByWeek] = {}
        self.verses_read_in = False

    def process(self):
        for folder in self.folders[:1]:
            pp = ParatextProjectByWeek(os.path.join(self.parent_folder, folder))
            pp.process()
            pp.save_week_data()
            self.week_data[folder] = pp


# %%
