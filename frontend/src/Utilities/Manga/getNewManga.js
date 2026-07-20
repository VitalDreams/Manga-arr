function getNewManga(searchResult, payload) {
  const {
    rootFolderPath,
    qualityProfileId,
    metadataProfileId,
    tags
  } = payload;

  return {
    foreignMangaId: searchResult.foreignMangaId,
    title: searchResult.title,
    titleSlug: searchResult.title,
    overview: searchResult.overview || '',
    author: searchResult.author || '',
    artist: '',
    status: searchResult.status || '',
    demographic: '',
    year: searchResult.year || 0,
    totalVolumes: 0,
    totalChapters: 0,
    genres: [],
    tags: tags || [],
    coverUrl: searchResult.coverUrl || '',
    rootFolderPath,
    qualityProfileId,
    metadataProfileId,
    monitored: true
  };
}

export default getNewManga;
