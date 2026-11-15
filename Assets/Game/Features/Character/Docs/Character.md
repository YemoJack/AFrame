# Character feature

The domain assembly owns movement commands, state and events. The Unity assembly owns the module asset, input adapter and view.

Movement is sent as `MoveCharacterCommand`. The module publishes `CharacterMovedEvent`, and the view reacts to that event.

The feature has no dependency on a concrete camera, physics engine, save format or network provider.
