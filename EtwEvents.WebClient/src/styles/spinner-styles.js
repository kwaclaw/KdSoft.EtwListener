import { css } from '@kdsoft/lit-mvvm';

export default css`

.spinning {
  animation-play-state: running;
  position: relative;
  animation: 1s linear infinite spinner;
}

@keyframes spinner {
  0% {
    color:var(--txt-color, rgb(160, 174, 192));
  }

  33% {
    color: red;
  }

  66% {
    color: yellow;
  }
}

`;
