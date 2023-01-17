import { css } from '@kdsoft/lit-mvvm';

export default css`

/* Styled Checkbox */

/* ReSharper disable CssNotResolved */

/* See https://codepen.io/paulobrien/pen/ZrKeaQ */

.kds-checkbox {
  width: 1.2rem;
  height: 1.2rem;
  -webkit-appearance: none;
  appearance: none;
  -webkit-print-color-adjust: exact;
  color-adjust: exact;
  -webkit-user-select: none;
  user-select: none;
  padding: 0.15rem 0.3rem;
  border: 1px solid #c8c8c8;
  border-radius: 0.2rem;
  cursor: pointer;
  background-image: url("data:image/svg+xml;charset=UTF-8,%3csvg version='1.1' id='Layer_1' xmlns='http://www.w3.org/2000/svg' xmlns:xlink='http://www.w3.org/1999/xlink' x='0px' y='0px' width='512px' height='512px' viewBox='-64 -64 640 640' style='enable-background:new 0 0 512 512;fill: %23179bd7' xml:space='preserve'%3e%3cpath d='M448,71.9c-17.3-13.4-41.5-9.3-54.1,9.1L214,344.2l-99.1-107.3c-14.6-16.6-39.1-17.4-54.7-1.8 c-15.6,15.5-16.4,41.6-1.7,58.1c0,0,120.4,133.6,137.7,147c17.3,13.4,41.5,9.3,54.1-9.1l206.3-301.7 C469.2,110.9,465.3,85.2,448,71.9z'/%3e%3c/svg%3e");
  background-repeat: no-repeat;
  background-size: 0;
  background-color: #fff;
  background-position: 50% 50%;
  transition: .3s ease;
}

.kds-checkbox:active {
  background-color: #ddd;
}

.kds-checkbox:focus {
  box-shadow: 0 0 0 2px rgba(66, 153, 225, 0.5);
}

.kds-checkbox:checked {
  background-size: cover;
}

.kds-checkbox:disabled {
  border: 1px solid gray;
  background-color: lightgray;
}

`;
