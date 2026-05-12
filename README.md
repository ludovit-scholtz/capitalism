# Capitalism online game

This is attempt to create online mmorpg version of the capitalism game.

## Product roadmap

[ROADMAP.md](ROADMAP.md)

## Security configuration

- Override `Jwt:SigningKey` with a strong secret via environment variable `Jwt__SigningKey` for any non-Development deployment of `projects/Api` and `projects/MasterApi`.
- Both APIs now fail fast at startup when `Jwt:SigningKey` is placeholder, empty/whitespace, or shorter than 32 characters outside Development.
