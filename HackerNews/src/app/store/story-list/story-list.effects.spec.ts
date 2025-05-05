import { cold, hot } from 'jasmine-marbles';
import { provideMockActions } from '@ngrx/effects/testing';
import { provideMockStore } from '@ngrx/store/testing';
import { TestBed } from '@angular/core/testing';
import { StoryListEffects } from './story-list.effects';
import {
  storyListLoadNextPage,
  storyListFetchSuccess,
  storyListChangePage,
  storyListPageOnlyChange
} from './story-list.actions';
import { StoryService } from '../../services/story.service';
import { of } from 'rxjs';

describe('StoryListEffects', () => {
  let actions$: any;
  let effects: StoryListEffects;
  let storyService: jasmine.SpyObj<StoryService>;

  beforeEach(() => {
    storyService = jasmine.createSpyObj('StoryService', ['getNextPage']);

    TestBed.configureTestingModule({
      providers: [
        StoryListEffects,
        provideMockActions(() => actions$),
        provideMockStore({
          initialState: {
            stories: {
              stories: [],
              displayedStories: [],
              loading: false,
              activeSearchQuery: '',
              currentPage: 0,
              totalPages: 0,
              currentHead: undefined,
              nextHead: undefined
            }
          }
        }),
        { provide: StoryService, useValue: storyService }
      ]
    });

    effects = TestBed.inject(StoryListEffects);
  });

  it('dispatches storyListFetchSuccess with page when storyListLoadNextPage action is received', () => {
    const page = {
      items: [{ id: 1 } as any],
      currentHead: 123,
      nextHead: 122
    };
    storyService.getNextPage.and.returnValue(of(page));

    actions$ = hot('-a-', { a: storyListLoadNextPage({}) });
    const expected = cold('-b-', { b: storyListFetchSuccess({ storyPage: page }) });

    expect(effects.loadStories$).toBeObservable(expected);
  });

  it('changePage$ emits storyListPageOnlyChange when cache suffices', () => {
    // nextHead is undefined so no needMore
    actions$ = hot('-a-', { a: storyListChangePage({ direction: 'next' }) });
    const expected = cold('-b-', { b: storyListPageOnlyChange({ newPage: 1 }) });

    expect(effects.changePage$).toBeObservable(expected);
  });
});
