import { selectAllStories, selectCanGoNext, selectCanGoPrevious } from './story-list.selectors';
import { StoryState } from './story-list.reducer';

describe('Story selectors', () => {
  const baseState: StoryState = {
    stories: Array.from({ length: 30 }, (_, i) => ({ id: i } as any)),
    displayedStories: Array.from({ length: 20 }, (_, i) => ({ id: i } as any)),
    loading: false,
    activeSearchQuery: '',
    currentPage: 1,
    totalPages: 2,
    currentHead: 0,
    nextHead: 0,
    hasMoreStories: true
  };

  const appState = { stories: baseState };

  it('selectAllStories returns full list', () => {
    expect(selectAllStories(appState).length).toBe(30);
  });

  it('selectCanGoPrevious true when page > 0', () => {
    expect(selectCanGoPrevious(appState)).toBeTrue();
  });

  it('selectCanGoPrevious false when on the first page', () => {
    const firstPageState = {
      stories: {
        ...baseState,
        currentPage: 0,
      },
    };

    expect(selectCanGoPrevious(firstPageState)).toBeFalse();
  });

  it('selectCanGoNext true when current page is full and not last', () => {
    // baseState is page  1 of 2 with 20 items shown, should meet selector conditions
    expect(selectCanGoNext(appState)).toBeTrue();
  });

  it('selectCanGoNext false when on last page and hasMoreStories is false', () => {
    const lastPageState = { ...appState, stories: { ...baseState, currentPage: 1, hasMoreStories: false } };
    expect(selectCanGoNext(lastPageState)).toBeFalse();
  });
});
