import { createAction, props } from "@ngrx/store";
import { StoryPage } from "../../models/story-page";

export const storyListLoadNextPage = createAction(
  '[Story List] Load Next Page',
  props<{ searchQuery?: string, lastId?: number, currentHead?: number, nextHead?: number, pageSize?: number }>()
);

export const storyListSearch = createAction(
  '[Story List] Search',
  props<{ searchQuery?: string, lastId?: number, currentHead?: number, nextHead?: number, pageSize?: number }>()
);

export const storyListFetchSuccess = createAction(
  '[Story List] Fetch Success',
  props<{ storyPage: StoryPage, newPage?: number }>()
);

export const storyListFetchFail = createAction(
  '[Story List] Fetch Fail',
  props<{ error: unknown }>()
);

export const storyListChangePage = createAction(
  '[Story List] Change Page',
  props<{ direction: 'next' | 'prev' }>()
);

export const storyListPageOnlyChange = createAction(
  '[Story List] Page Only Change',
  props<{ newPage: number }>()
);
