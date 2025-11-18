# ?? Sudoku Game - Blazor Edition

A modern, feature-rich Sudoku puzzle game built with **Blazor Server** (.NET 10) featuring clean architecture, real-time validation, and a stunning neon UI.

![Blazor](https://img.shields.io/badge/Blazor-512BD4?style=for-the-badge&logo=blazor&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&logo=.net&logoColor=white)
![C#](https://img.shields.io/badge/C%23-14.0-239120?style=for-the-badge&logo=c-sharp&logoColor=white)

## ? Features

### ?? Game Features
- **Multiple Difficulty Levels**: Easy, Medium, and Hard puzzles
- **Smart Puzzle Generation**: Ensures unique solutions for each puzzle
- **Real-time Conflict Detection**: Automatically highlights duplicate numbers in rows, columns, and 3�3 boxes
- **Intelligent Hint System**: Suggests the next logical move using advanced solving techniques
- **Auto-Solve**: Instantly solves the current puzzle
- **Validation**: Check if your current solution is valid
- **Clear All**: Reset all user-entered values while keeping the original puzzle
- **Keyboard Support**: Enter numbers 1-9, use Backspace/Delete to clear cells
- **Mouse Interaction**: Click to select cells, drag to change selection

### ?? UI/UX Features
- **Animated Cell Selection**: Blue pulsing animation on selected cells
- **Conflict Highlighting**: Red pulsing animation on conflicting numbers
- **Row/Column Highlighting**: Soft highlights for same row/column as selected cell
- **Neon Navigation Menu**: Cyberpunk-style glowing, flashing navigation
- **Responsive Design**: Works on desktop, tablet, and mobile devices
- **Professional Color Schemes**: Gradient buttons with hover effects
- **Interactive Timeline**: Visual history of Sudoku from 1783 to present

## ??? Architecture

This project follows **Clean Architecture** principles with **SOLID** design patterns:

```
Sudoku/
??? Domain/                      # Core business entities
?   ??? Board.cs                # 9�9 Sudoku board model
?   ??? Cell.cs                 # Individual cell with value and state
?   ??? Position.cs             # Row/column position structure
?
??? Application/                # Business logic layer
?   ??? Interfaces/             # Abstractions
?   ?   ??? ISudokuGenerator.cs
?   ?   ??? ISudokuSolver.cs
?   ?   ??? ISudokuValidator.cs
?   ?   ??? ISudokuHintProvider.cs
?   ??? Services/
?       ??? SudokuService.cs    # Main game coordinator
?
??? Infrastructure/             # Implementation layer
?   ??? SudokuGenerator.cs     # Puzzle generation algorithm
?   ??? SudokuSolver.cs        # Backtracking solver
?   ??? SudokuValidator.cs     # Rule validation logic
?   ??? SudokuHintProvider.cs  # Smart hint suggestions
?
??? Components/                 # Blazor UI layer
    ??? Pages/
    ?   ??? SudokuBoard.razor   # Main game board
    ?   ??? About.razor         # About page with history
    ??? Layout/
        ??? MainLayout.razor    # App layout
        ??? NavMenu.razor       # Navigation menu
```

### Design Patterns Used
- **Dependency Injection**: All services are injected via DI container
- **Interface Segregation**: Small, focused interfaces for each responsibility
- **Single Responsibility**: Each class has one clear purpose
- **Strategy Pattern**: Different difficulty levels use different removal strategies
- **Service Layer**: Business logic separated from presentation

## ?? Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Visual Studio 2026 or Visual Studio Code
- Modern web browser (Chrome, Edge, Firefox, Safari)

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/bgard68/Net10Sudoku
   cd Sudoku
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Build the project**
   ```bash
   dotnet build
   ```

4. **Run the application**
   ```bash
   dotnet run --project Sudoku
   ```

5. **Open your browser**
   Navigate to `https://localhost:7086` or `http://localhost:5260`

### Using Visual Studio
1. Open `Sudoku.sln`
2. Press `F5` or click **Start Debugging**
3. The app will launch in your default browser

## ?? How to Play

1. **Start a New Game**: Click **Easy**, **Medium**, or **Hard** to generate a new puzzle
2. **Select a Cell**: Click any empty cell to select it
3. **Enter a Number**: 
   - Click number buttons (1-9) at the bottom
   - OR press number keys (1-9) on your keyboard
4. **Clear a Cell**: 
   - Click the **Clear** button
   - OR press Backspace/Delete on your keyboard
5. **Get Help**:
   - **Hint**: Get a smart suggestion for the next move
   - **Validate**: Check if your current solution has conflicts
   - **Solve**: Automatically solve the entire puzzle
   - **Clear All**: Remove all your entries and start over

### Game Rules
- Each **row** must contain digits 1-9 exactly once
- Each **column** must contain digits 1-9 exactly once
- Each **3�3 box** must contain digits 1-9 exactly once
- **Conflicting numbers** are highlighted in red automatically

## ?? Algorithm Details

### Puzzle Generation
1. **Initialize**: Create empty 9�9 grid
2. **Fill Diagonal Boxes**: Randomly fill the three diagonal 3�3 boxes (no conflicts possible)
3. **Solve**: Use backtracking to complete the puzzle
4. **Remove Numbers**: 
   - Easy: Remove ~40 numbers
   - Medium: Remove ~50 numbers
   - Hard: Remove ~55 numbers
5. **Verify Uniqueness**: Ensure only one solution exists

### Solving Algorithm
- **Backtracking**: Recursive depth-first search
- **Constraint Propagation**: Eliminates invalid candidates early
- **Validation**: Checks row, column, and box constraints

### Hint System
- **Naked Singles**: Cells with only one possible value
- **Hidden Singles**: Numbers that can only go in one cell within a row/column/box
- **Logical Deduction**: Uses advanced techniques to suggest moves

## ?? UI Components

### Navigation Menu
- **Neon Glow Effect**: Cyberpunk-style fluorescent colors
- **Animations**: Pulsing, flashing, and color-shifting effects
- **Colors**:
  - Default: Cyan (`#00ffff`)
  - Active: Magenta (`#ff00ff`)
  - Hover: Yellow (`#ffff00`)

### Game Board
- **9�9 Grid**: 52�52px cells with clear 3�3 box separators
- **Given Numbers**: Bold with light background (immutable)
- **User Numbers**: Regular weight, editable
- **Selected Cell**: Blue pulsing animation
- **Conflicts**: Red pulsing animation
- **Row/Column Highlight**: Soft gradient overlay

### Buttons
- **Difficulty Buttons**: Color-coded gradients
  - Easy: Green gradient
  - Medium: Orange gradient
  - Hard: Red gradient
- **Action Buttons**: Purple/pink/blue gradients with hover effects
- **Number Pad**: Blue gradient with hover states

## ?? About Page

Interactive timeline showing Sudoku's evolution:
- **1783**: Leonhard Euler's Latin Squares
- **1979**: Howard Garns creates "Number Place"
- **1984**: Nikoli publishes as "Sudoku" in Japan
- **2004**: Wayne Gould brings it to The Times
- **2005**: Worldwide phenomenon

## ??? Technology Stack

- **Framework**: Blazor Server (.NET 10)
- **Language**: C# 14.0
- **UI**: Razor Components with CSS
- **Architecture**: Clean Architecture + SOLID principles
- **State Management**: Scoped services via DI
- **Styling**: Custom CSS with animations and gradients

## ?? Dependencies

- `Microsoft.AspNetCore.Components.Web` - Blazor components
- `Microsoft.AspNetCore.Components.Server` - Server-side Blazor
- No external puzzle libraries - all algorithms custom-built!

## ?? Contributing

Contributions are welcome! Feel free to:
1. Fork the repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## ?? License

This project is open source and available under the [MIT License](LICENSE).

## ?? Future Enhancements

- [ ] Timer and score tracking
- [ ] Leaderboard with best times
- [ ] Save/load game state
- [ ] More difficulty levels (Professional, Expert)
- [ ] Puzzle of the Day
- [ ] Dark/Light theme toggle
- [ ] Print puzzle feature
- [ ] Undo/Redo functionality
- [ ] Pencil marks (notes) for candidates
- [ ] Mobile app (MAUI)

## ????? Author

Created with ?? using Blazor Server and Clean Architecture principles.

## ?? Acknowledgments

- Leonhard Euler for Latin Squares
- Howard Garns for inventing modern Sudoku
- Nikoli for popularizing it in Japan
- The Blazor team at Microsoft

---

**? If you like this project, please give it a star on GitHub! ?**
