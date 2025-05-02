export function paginate<T>(items: T[], pageSize: number, currentPage: number) {
  const startIndex = currentPage * pageSize;
  const endIndex = startIndex + pageSize;
  return {
    displayedItems: items.slice(startIndex, endIndex),
    totalPages: Math.ceil(items.length / pageSize)
  };
}
