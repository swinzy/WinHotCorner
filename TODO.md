# To do

Planned work and open questions. See [docs/technical.md](docs/technical.md) for how things work now.

## Translations

- [ ] **More languages.** English, Chinese (Simplified and Traditional) and Spanish (Latin American and Spain's) are done; see [docs/technical.md](docs/technical.md#translations) for what a new language needs.

## Multiple monitors experience improvement

- [ ] **Covered corners on Windows 10.** Sticky corners were measured on Windows 11, with monitors side by side and stacked vertically. To check: Windows 10.
- [ ] **Covered corners without stopping the pointer (opt-in).** A compromise for covered corners where nothing holds the pointer (no sticky corners, and *Expand hot corner area* off): the pointer is not stopped, so it just moves on to the other monitor. With this setting, the corner would still trigger when the pointer crosses the edge near that corner in the right direction, from this monitor out towards its top left, using the same pressure rules on the crossing movement; movement the other way (coming back from the other monitor) never triggers. As the pointer is not held, only a fast enough movement can build up the pressure. A preference in the control panel, off by default.
