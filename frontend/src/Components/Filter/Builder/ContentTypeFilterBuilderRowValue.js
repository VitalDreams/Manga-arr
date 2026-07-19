import React from 'react';
import FilterBuilderRowValue from './FilterBuilderRowValue';

const contentTypes = [
  { id: 0, name: 'Manga' },
  { id: 1, name: 'Manhwa' },
  { id: 2, name: 'Manhua' },
  { id: 3, name: 'Other' }
];

function ContentTypeFilterBuilderRowValue(props) {
  return (
    <FilterBuilderRowValue
      tagList={contentTypes}
      {...props}
    />
  );
}

export default ContentTypeFilterBuilderRowValue;
