import { inject, Injectable } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { of } from 'rxjs';
import { catchError, map, switchMap, withLatestFrom } from 'rxjs/operators';
import { StoryService } from '../../services/story.service';
import { AppState } from '../app.state';
import { storyListChangePage, storyListFetchFail, storyListFetchSuccess, storyListLoadNextPage, storyListPageOnlyChange, storyListSearch } from './story-list.actions';
import { selectStories } from './story-list.selectors';

@Injectable()
export class StoryListEffects {
  private actions$ = inject(Actions);
  private store = inject(Store<AppState>);
  private storyService = inject(StoryService);

  loadStories$ = createEffect(() =>
    this.actions$.pipe(
      ofType(storyListLoadNextPage, storyListSearch),
      switchMap((action) =>
        this.storyService.getNextPage(action.searchQuery, action.lastId, action.currentHead, action.nextHead, action.pageSize).pipe(
          map((storyPage) => {
            console.log(storyPage);
            return storyListFetchSuccess({ storyPage: storyPage })
          }),
          catchError((error) => {
            console.error('Error in loadStories$', error);
            return of(storyListFetchFail({ error }));
          })
        )
      )
    )
  );

  changePage$ = createEffect(() =>
    this.actions$.pipe(
      ofType(storyListChangePage),
      withLatestFrom(this.store.select(selectStories)),
      switchMap(([{ direction }, state]) => {
        const newPage = direction === 'next' ? state.currentPage + 1 : state.currentPage - 1;
        const startIndex = newPage * 20;

        const needsMore = startIndex >= state.stories.length && state.nextHead;

        if (needsMore) {
          const lastId = state.stories[state.stories.length - 1]?.id;

          return this.storyService.getNextPage(
            state.activeSearchQuery,
            lastId,
            state.currentHead,
            state.nextHead
          ).pipe(
            map(data => storyListFetchSuccess({ storyPage: data, newPage: newPage })),
            catchError((error) => of(storyListFetchFail({ error })))
          );
        }

        return of(storyListPageOnlyChange({ newPage }));
      })
    )
  );
}
