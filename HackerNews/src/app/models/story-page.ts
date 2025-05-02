import { Item } from "./story";

export interface StoryPage {
  items: Item[];
  currentHead: number;
  nextHead: number;
}
