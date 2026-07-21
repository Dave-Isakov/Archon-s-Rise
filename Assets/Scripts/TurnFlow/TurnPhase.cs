// The three phases of a turn (spec 2026-07-21). Strictly one-way:
// Explore -> Action -> End, then a new turn begins at Explore.
public enum TurnPhase { Explore, Action, End }
