import { StoryListReducer, StoryState } from './story-list.reducer';
import {
  storyListLoadNextPage,
  storyListSearch,
  storyListFetchSuccess,
  storyListPageOnlyChange
} from './story-list.actions';

describe('StoryListReducer', () => {
  const initialState: StoryState = {
    stories: [],
    displayedStories: [],
    loading: false,
    activeSearchQuery: '',
    currentPage: 0,
    totalPages: 0,
    hasMoreStories: false
  };

  it('sets loading true on LoadNextPage', () => {
    const newState = StoryListReducer(
      initialState,
      storyListLoadNextPage({})
    );
    expect(newState.loading).toBeTrue();
  });

  it('resets stories & sets query on Search', () => {
    const populated = { ...initialState, stories: [{ id: 1 } as any] };
    const newState = StoryListReducer(
      populated,
      storyListSearch({ searchQuery: 'angular' })
    );
    expect(newState.stories.length).toBe(0);
    expect(newState.activeSearchQuery).toBe('angular');
    expect(newState.loading).toBeTrue();
  });

  it('adds items & updates heads on FetchSuccess', () => {
    const action = storyListFetchSuccess({
      storyPage: {
        items: [{ id: 42 } as any],
        currentHead: 100,
        nextHead: 101,
        hasMoreStories: true
      }
    });
    const newState = StoryListReducer(initialState, action);
    expect(newState.stories.length).toBe(1);
    expect(newState.displayedStories[0].id).toBe(42);
    expect(newState.currentHead).toBe(100);
    expect(newState.nextHead).toBe(101);
    expect(newState.totalPages).toBe(1);
  });

  it('paginates correctly on PageOnlyChange', () => {
    const many = Array.from({ length: 45 }, (_, i) => ({ id: i } as any));
    const pagedState = { ...initialState, stories: many };
    const newState = StoryListReducer(
      pagedState,
      storyListPageOnlyChange({ newPage: 2 })
    );
    expect(newState.currentPage).toBe(2);
    // pageSize = 20 so page 2 starts at index 40
    expect(newState.displayedStories[0].id).toBe(40);
    expect(newState.displayedStories.length).toBe(5);
  });
});
