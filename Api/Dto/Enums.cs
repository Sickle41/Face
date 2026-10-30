namespace WarmachineClient.Api.Dto;

public enum UnitCategory
{
    Warcaster,
    Warlock,
    Master,
    Warjack,
    Warbeast,
    Horror,
    Unit,
    Solo,
    BattleEngine,
    Structure
}

public enum BaseSize
{
    Small,
    Medium,
    Large,
    ExtraLarge,
    Huge
}

public enum TerrainType
{
    Forest,
    Hill,
    Water,
    Obstacle,
    Obstruction,
    Rubble,
    DustDevil,
    DenseFog,
    Crater,
    Storm,
    TallGrass,
    Trench,
    Hazard
}

public enum TerrainShape
{
    Rectangle,
    Circle
}

public enum MovementEffect
{
    Normal,
    Difficult,
    Impassable
}
