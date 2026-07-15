function getProxiedCoverUrl(coverUrl) {
  if (coverUrl && coverUrl.startsWith('https://uploads.mangadex.org/')) {
    return `/api/v1/manga/cover?url=${encodeURIComponent(coverUrl)}`;
  }
  return coverUrl;
}

export default getProxiedCoverUrl;
