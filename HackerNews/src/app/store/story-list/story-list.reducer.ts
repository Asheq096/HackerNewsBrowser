import { createReducer, on } from "@ngrx/store";
import { Item } from "../../models/story";
import { paginate } from "../utils";
import { storyListFetchFail, storyListFetchSuccess, storyListLoadNextPage, storyListPageOnlyChange, storyListSearch } from "./story-list.actions";

export interface StoryState {
  stories: Item[];
  displayedStories: Item[]; // Stories to show on current page
  loading: boolean;
  currentHead?: number;
  nextHead?: number;
  activeSearchQuery: string;
  currentPage: number;
  totalPages: number;
  hasMoreStories: boolean;
}

const initialState: StoryState = {
  stories: [],
  displayedStories: [],
  loading: false,
  activeSearchQuery: '',
  currentPage: 0,
  totalPages: 0,
  hasMoreStories: false
}

export const StoryListReducer = createReducer(
  initialState,

  on(
    storyListLoadNextPage,
    (state): StoryState => ({
      ...state,
      loading: true
    })
  ),

  on(
    storyListSearch,
    (state, action): StoryState => ({
      ...state,
      loading: true,
      stories: [],
      displayedStories: [],
      currentPage: 0,
      currentHead: undefined,
      nextHead: undefined,
      activeSearchQuery: action.searchQuery ?? ''
    })
  ),

  on(
    storyListFetchSuccess,
    (state, action): StoryState => {
      return {
        ...state,
        stories: [...state.stories, ...action.storyPage.items],
        loading: false,
        currentPage: action.newPage ?? state.currentPage,
        currentHead: action.storyPage.currentHead,
        nextHead: action.storyPage.nextHead,
        displayedStories: action.storyPage.items,
        totalPages: Math.ceil((state.stories.length + action.storyPage.items.length) / 20),
        hasMoreStories: action.storyPage.hasMoreStories
      }
    }
  ),

  on(
    storyListFetchFail,
    (state): StoryState => ({
      ...state,
      loading: false
    })
  ),

  on(
    storyListPageOnlyChange,
    (state, { newPage }) => {
      const { displayedItems, totalPages } = paginate(state.stories, 20, newPage);

      return {
        ...state,
        currentPage: newPage,
        displayedStories: displayedItems,
        totalPages
      };
    }
  )
)
