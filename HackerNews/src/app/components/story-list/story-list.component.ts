import { Component, OnInit, Signal } from '@angular/core';
import { Store } from '@ngrx/store';
import { Item } from '../../models/story';
import { StoryService } from '../../services/story.service';
import { AppState } from '../../store/app.state';
import { storyListChangePage, storyListLoadNextPage, storyListSearch } from '../../store/story-list/story-list.actions';
import { selectActiveSearchQuery, selectCanGoNext, selectCanGoPrevious, selectCurrentPage, selectDisplayedStories, selectIsLoading } from '../../store/story-list/story-list.selectors';

@Component({
  selector: 'app-story-list',
  standalone: false,
  templateUrl: './story-list.component.html',
  styleUrl: './story-list.component.css'
})
export class StoryListComponent implements OnInit {
  searchQuery: string = '';
  displayedColumns: string[] = ['id', 'title', 'by', 'time'];
  readonly pageSize: number = 20;

  isLoading: Signal<boolean>;
  displayedStories: Signal<Item[]>;
  activeSearchQuery: Signal<string>;
  canGoPrevious: Signal<boolean>;
  canGoNext: Signal<boolean>;
  currentPage: Signal<number>;

  constructor(private storyService: StoryService, private readonly store: Store<AppState>) {
    this.isLoading = this.store.selectSignal(selectIsLoading);
    this.displayedStories = this.store.selectSignal(selectDisplayedStories);
    this.activeSearchQuery = this.store.selectSignal(selectActiveSearchQuery);
    this.canGoPrevious = this.store.selectSignal(selectCanGoPrevious);
    this.canGoNext = this.store.selectSignal(selectCanGoNext);
    this.currentPage = this.store.selectSignal(selectCurrentPage);
  }

  ngOnInit() {
    // load initial stories
    this.store.dispatch(storyListLoadNextPage({}));
  }

  search() {
    this.store.dispatch(storyListSearch({ searchQuery: this.searchQuery }));
  }

  clearSearch() {
    this.searchQuery = '';
    this.search();
  }

  changePage(direction: 'next' | 'prev') {
    this.store.dispatch(storyListChangePage({ direction }));
  }
}
