import { ComponentFixture, TestBed } from '@angular/core/testing';
import { StoryListComponent } from './story-list.component';
import { provideMockStore, MockStore } from '@ngrx/store/testing';
import { storyListLoadNextPage, storyListSearch } from '../../store/story-list/story-list.actions';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatInputModule } from '@angular/material/input';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';

describe('StoryListComponent', () => {
  let component: StoryListComponent;
  let fixture: ComponentFixture<StoryListComponent>;
  let store: MockStore;

  const initialState = {
    stories: {
      stories: [],
      displayedStories: [],
      loading: false,
      activeSearchQuery: '',
      currentPage: 0,
      totalPages: 0
    }
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [StoryListComponent],
      imports: [
        FormsModule,
        MatButtonModule,
        MatInputModule,
        MatCardModule,
        MatFormFieldModule,
        MatTableModule,
        MatProgressSpinnerModule,
        MatIconModule,
        BrowserAnimationsModule
      ],
      providers: [provideMockStore({ initialState }), provideHttpClientTesting(), provideHttpClient()]
    }).compileComponents();

    store = TestBed.inject(MockStore);
    spyOn(store, 'dispatch').and.callThrough();

    fixture = TestBed.createComponent(StoryListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('dispatches LoadNextPage on init', () => {
    fixture.detectChanges(); // triggers ngOnInit
    expect(store.dispatch).toHaveBeenCalledWith(storyListLoadNextPage({}));
  });

  it('dispatches Search with ngModel value', () => {
    component.searchQuery = 'webdev';
    component.search();
    expect(store.dispatch).toHaveBeenCalledWith(
      storyListSearch({ searchQuery: 'webdev' })
    );
  });
});
