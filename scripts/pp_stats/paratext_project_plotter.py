from datetime import datetime

import numpy as np
import pandas as pd
import plotly.graph_objects as go
from plotly import express as px
from plotly.subplots import make_subplots

pd.options.plotting.backend = "plotly"


# %%
def plot_combined_progress(
    combined_df: pd.DataFrame,
    draft_events: list[datetime],
    title: str = None,
    year_start: int = None,
) -> go.Figure:

    cols_to_plot = [
        "firstDraft_sum",
        "majorEdit_sum",
        "moderateEdit_sum",
        "minorEdit_sum",
        "notesChange_sum",
    ]
    combined_df.columns = [col.replace("_sum", "") for col in combined_df.columns]
    cols_to_plot = [col.replace("_sum", "") for col in cols_to_plot]

    start_time = combined_df.loc[0, "dateTime"].tz_localize(None)
    draft_event_weeks_since_start = [
        (event.replace(tzinfo=None) - start_time).days // 7 for event in draft_events
    ]

    # create 3 axis plot vertically stacked with reduced spacing
    fig = make_subplots(rows=3, cols=1, shared_xaxes=True, vertical_spacing=0.01)

    # Add dark grey vertical lines for each year
    if len(combined_df) > 0:
        # Extract years present in the data
        years, week_inds = np.unique(
            pd.DatetimeIndex(combined_df["dateTime"]).year, return_index=True
        )
        weeks = combined_df.index[week_inds]

        # Add vertical lines for the first day of each year (except possibly the first)
        for year, week in zip(years, weeks):
            # Add vertical line on each subplot
            fig.add_vline(
                x=week,
                line=dict(color="dimgray", width=1, dash="solid"),
                opacity=0.7,
                row="all",
                col=1,
            )

            # Add vertical year text for each year transition
            fig.add_annotation(
                x=week,
                y=1.02,
                yref="paper",
                text=str(year),
                textangle=-90,
                showarrow=False,
                font=dict(size=10, color="dimgray"),
                xanchor="center",
                yanchor="bottom",
                row="all",
                col=1,
            )

    # Add bold red vertical lines for draft events
    for week in draft_event_weeks_since_start:
        fig.add_vline(
            x=week,
            line=dict(color="red", width=2, dash="solid"),
            opacity=0.8,
            row="all",
            col=1,
            layer="below",
        )

    # Top subplot: First Draft
    fig.add_trace(
        go.Scatter(
            x=combined_df.index, y=combined_df["firstDraft"], name="First Draft"
        ),
        row=1,
        col=1,
    )

    # Middle subplot: All edits
    fig.add_trace(
        go.Scatter(x=combined_df.index, y=combined_df["majorEdit"], name="Major Edit"),
        row=2,
        col=1,
    )
    fig.add_trace(
        go.Scatter(
            x=combined_df.index, y=combined_df["moderateEdit"], name="Moderate Edit"
        ),
        row=2,
        col=1,
    )
    fig.add_trace(
        go.Scatter(x=combined_df.index, y=combined_df["minorEdit"], name="Minor Edit"),
        row=2,
        col=1,
    )

    # Bottom subplot: Notes
    fig.add_trace(
        go.Scatter(
            x=combined_df.index, y=combined_df["notesChange"], name="Notes Change"
        ),
        row=3,
        col=1,
    )

    # Update y-axes to log scale
    fig.update_yaxes(type="log", row=1, col=1, title="First Draft", fixedrange=True)
    fig.update_yaxes(type="log", row=2, col=1, title="Edits", fixedrange=True)
    fig.update_yaxes(type="log", row=3, col=1, title="Notes", fixedrange=True)

    # Add x-axis title to the bottom subplot
    if "RemoveIdle" in combined_df.index.name:
        fig.update_xaxes(
            title="Weeks since beginning (idle weeks removed)", row=3, col=1
        )
    else:
        fig.update_xaxes(title="Weeks since beginning", row=3, col=1)

    # Set title and layout
    if title:
        fig.update_layout(title_text=title)

    fig.update_layout(
        height=600,
        showlegend=True,
        margin=dict(t=30, b=30, l=30, r=30),
        legend=dict(
            x=0.02,
            y=0.98,
            xanchor="left",
            yanchor="top",
            bgcolor="rgba(255, 255, 255, 0.7)",
            bordercolor="rgba(0, 0, 0, 0.1)",
            borderwidth=1,
        ),
    )

    # Make the y-axis titles more compact
    fig.update_yaxes(titlefont=dict(size=10), row=1, col=1)
    fig.update_yaxes(titlefont=dict(size=10), row=2, col=1)
    fig.update_yaxes(titlefont=dict(size=10), row=3, col=1)

    # Set the default zoom for the X axis to start at the year_start
    if year_start is not None:
        start_week = max(0, (datetime(year_start, 1, 1) - start_time).days // 7)
        end_week = max(combined_df.index)
        fig.update_xaxes(range=[start_week, end_week])

    return fig


# %%
