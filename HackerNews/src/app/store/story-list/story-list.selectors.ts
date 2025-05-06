import { createSelector } from "@ngrx/store";
import { AppState } from "../app.state";
import { StoryState } from "./story-list.reducer";

export const selectStories = (state: AppState) => state.stories;

export const selectAllStories = createSelector(
  selectStories,
  (state: StoryState) => state.stories
);

export const selectIsLoading = createSelector(
  selectStories,
  (state: StoryState) => state.loading
);

export const selectActiveSearchQuery = createSelector(
  selectStories,
  (state: StoryState) => state.activeSearchQuery
);

export const selectCurrentPage = createSelector(
  selectStories,
  (state: StoryState) => state.currentPage
);

export const selectDisplayedStories = createSelector(
  selectStories,
  (state: StoryState) => state.displayedStories
);

export const selectCanGoPrevious = createSelector(
  selectStories,
  (state: StoryState) => state.currentPage > 0
);

export const selectCanGoNext = createSelector(
  selectStories,
  (state: StoryState) => {
    return state.currentPage < state.totalPages - 1 || state.hasMoreStories;
  }
);
