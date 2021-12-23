import { css } from '@kdsoft/lit-mvvm';

export default css`

  .btn {
    padding-top: 0.25rem;
    padding-bottom: 0.25rem;
    padding-left: 0.5rem;
    padding-right: 0.5rem;
    font-weight: 700
}
  .btn-gray {
    --tw-bg-opacity: 1;
    background-color: rgb(107 114 128 / var(--tw-bg-opacity));
    --tw-text-opacity: 1;
    color: rgb(255 255 255 / var(--tw-text-opacity))
}
  .btn-gray:hover {
    --tw-bg-opacity: 1;
    background-color: rgb(75 85 99 / var(--tw-bg-opacity))
}
  .btn-gray:disabled {
    --tw-text-opacity: 1;
    color: rgb(156 163 175 / var(--tw-text-opacity))
}
`;
