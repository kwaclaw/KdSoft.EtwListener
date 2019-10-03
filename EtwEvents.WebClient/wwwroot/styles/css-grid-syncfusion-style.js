
import { css } from './css-tag.js';

// eslint-disable-next-line import/prefer-default-export
export const SyncFusionGridStyle = css`
/* CSS Grid SyncFusion Style */

.sfg-container {
    display: grid;
    grid-auto-rows: max-content;
    grid-auto-flow: row;
    font-size: 12px;
    border: 1px solid #c8c8c8;
    /*background-color: lightgray;*/
    /*grid-gap: 1px;*/
    overflow: auto;
}

.sfg-container > div:hover {
    overflow: visible;
}

/* this div is ignored as a child, due to display:contents, so it is excluded from the grid layout */

.sfg-row, .sfg-header-row {
    display: contents;
}

.sfg-row > div {
    padding: 8px 4px;
    background-color: white;
    outline: 1px solid #c8c8c8;
    overflow: hidden;
    white-space: nowrap;
    text-overflow: ellipsis;
}

/* alternating row background color 

.sfg-row:nth-child(2n) > div {
    background-color: #f7f7f7;
}
*/

/* placed last, because we want this selector to run last for the row */

.sfg-row:hover > div {
    background-color: lightblue;
}

.sfg-row > div:hover {
    overflow: visible;
    white-space: unset;
}

.sfg-row > .sfg-menu {
    text-align: center;
    text-justify: auto;
    overflow: unset;
    white-space: unset;
    text-overflow: unset;
    padding-left: 5px;
    padding-right: 5px;
}

.sfg-row .edit-icon {
    cursor: pointer;
    margin: 4px;
}

.sfg-row .delete-icon {
    cursor: pointer;
    margin: 4px;
}

.sfg-row .action-icon {
    cursor: pointer;
    margin: 4px;
}

.sfg-centeredItem {
    text-align: center;
}

.sfg-header {
    background-color: white;
    font-size: 13px;
    font-weight: bold;
    outline: 1px solid #c8c8c8;
    position: -webkit-sticky;
    position: sticky;
    top: 0;
    min-height: 30px;
    max-height: 160px;
    text-align: center;
    padding: 5px;
    /*overflow: hidden;
    white-space: nowrap;
    text-overflow: ellipsis;*/
}

.sfg-header.sfg-menuHeader {
    z-index: 100;
}

.sfg-centeredHeaderCell {
    height: 100%;
    display: flex;
    align-items: center; /* vertical */
    justify-content: center; /* horizontal */
}

.sfg-stickyActionColumn {
    position: -webkit-sticky;
    position: sticky;
    right: 0;
}

.sfg-rotated {
    writing-mode: vertical-lr;
    text-orientation: sideways;
    max-height: 160px;
}

/* END CSS Grid SyncFusion Style */
`;
