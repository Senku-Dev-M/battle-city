using System;

namespace Application.DTOs;

// Represents a cell in the game map
public record MapCellDto(int X, int Y, bool IsDestructible, bool IsDestroyed);
