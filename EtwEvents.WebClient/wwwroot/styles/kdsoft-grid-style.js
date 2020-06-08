
import { css } from '../lib/@kdsoft/lit-mvvm.js';

// eslint-disable-next-line import/prefer-default-export
export const KdSoftGridStyle = css`
/* CSS Grid KdSoft Style */

.kds-container {
    display: grid;
    grid-auto-rows: max-content;
    grid-auto-flow: row;
    font-size: 12px;
    border: 1px solid #c8c8c8;
    /*background-color: lightgray;*/
    /*grid-gap: 1px;*/
    overflow: auto;
}

.kds-container > div:hover {
    overflow: visible;
}

/* this div is ignored as a child, due to display:contents, so it is excluded from the grid layout */

.kds-row, .kds-header-row {
    display: contents;
}

.kds-row > div {
    padding: 8px 4px;
    background-color: white;
    outline: 1px solid #c8c8c8;
    overflow: hidden;
    white-space: nowrap;
    text-overflow: ellipsis;
}

/* alternating row background color 

.kds-row:nth-child(2n) > div {
    background-color: #f7f7f7;
}
*/

/* placed last, because we want this selector to run last for the row */

.kds-row:hover > div {
    background-color: lightblue;
}

.kds-row > div:hover {
    overflow: visible;
    white-space: unset;
}

.kds-row > .kds-menu {
    text-align: center;
    text-justify: auto;
    overflow: unset;
    white-space: unset;
    text-overflow: unset;
    padding-left: 5px;
    padding-right: 5px;
}

.kds-row .edit-icon {
    cursor: pointer;
    margin: 4px;
}

.kds-row .delete-icon {
    cursor: pointer;
    margin: 4px;
}

.kds-row .action-icon {
    cursor: pointer;
    margin: 4px;
}

.kds-centeredItem {
    text-align: center;
}

.kds-header {
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

.kds-header.kds-menuHeader {
    z-index: 100;
}

.kds-centeredHeaderCell {
    height: 100%;
    display: flex;
    align-items: center; /* vertical */
    justify-content: center; /* horizontal */
}

.kds-stickyActionColumn {
    position: -webkit-sticky;
    position: sticky;
    right: 0;
}

.kds-rotated {
    writing-mode: vertical-lr;
    text-orientation: sideways;
    max-height: 160px;
}

/* END CSS Grid KdSoft Style */
`;
