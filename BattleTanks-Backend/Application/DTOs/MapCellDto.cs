using System;

namespace Application.DTOs;

// Represents a cell in the game map including its type
// Type codes mirror the classic Battle City layout:
// 0 = empty, 1 = destructible block, 2 = indestructible block, 3 = base
public record MapCellDto(int X, int Y, int Type, bool IsDestructible, bool IsDestroyed);
