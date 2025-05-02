import { HttpClient, HttpParams } from '@angular/common/http';
import { Inject, Injectable } from '@angular/core';
import { Item } from '../models/story';
import { StoryPage } from '../models/story-page';

@Injectable({
  providedIn: 'root'
})
export class StoryService {
  private getStoryApiUrl: string = "https://localhost:7111/api" + "/HackerNews";

  constructor(private http: HttpClient) { }

  getNextPage(searchQuery?: string, lastId?: number, currentHead?: number, nextHead?: number, pageSize: number = 20) {
    let params = new HttpParams();

    if (lastId)
      params = params.set('startAfterId', lastId);
    if (currentHead)
      params = params.set('currentHead', currentHead);
    if (nextHead)
      params = params.set('nextHead', nextHead);
    if (searchQuery)
      params = params.set('searchQuery', searchQuery);
    params = params.set('pageSize', pageSize);

    return this.http.get<StoryPage>(this.getStoryApiUrl + "/GetStoriesWithLinks", { params: params });
  }
}
