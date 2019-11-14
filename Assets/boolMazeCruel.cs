using UnityEngine;
using System.Linq;
using System.Text.RegularExpressions;
using KMHelper;
using System.Collections;

/// <summary>
/// A maze module that requires the defuser to navigate a grid of logic gates based on a displayed number which changes after each move. 
/// If the displayed number when converted to 2-digit binary, would return a 1 when passed through the logic gate, then the space is a legal move.
/// The gates in the grid are Nor, Xor, Or, and And, and the controls include a display screen which shows the current number to be tested, 
/// 4 movement buttons (U R L D) to move up right left and down respectively in the grid, and a stuck? button which should only be used if the defuser
/// is in a deadend. At this moment the defuser can press the button to change the number in the display so they may move again. If the defuser 
/// tries to move into an illegal square (i.e. the logic gate would return 0) then the defuser gains a strike and does not move. If the defuser attempts
/// to use the Stuck? button when they still have a legal move (even backwards!) then they gain a strike and are reset back to the start. 
/// The starting and ending position are set by the (3rd,4th) and (5th,6th) characters of the serial, in the format of (row,col) where the top left square
/// is (0,0). A = 1, B = 2, C = 3, ... and any letter > 9 should be taken modulo 10 so it stays within the grid. 
/// </summary>
public class boolMazeCruel : MonoBehaviour
{
    public KMBombInfo BombInfo;
    public KMBombModule BombModule;
    public KMAudio KMAudio;
    public KMSelectable ButtonUp;
    public KMSelectable ButtonLeft;
    public KMSelectable ButtonRight;
    public KMSelectable ButtonDown;
    public KMSelectable ButtonStuck;
    public KMSelectable ButtonReset;
    public TextMesh NumDisplay;
    

    //Initialize Variables
    private int gridPosRow = 0;
    private int gridPosCol = 0;
    private int smallgridPosRow = 0;
    private int smallgridPosCol = 0;
    private int correctgridrow = 0;
    private int correctgridcol = 0;
    private int booldisplay = 0;
    private int initrow = 0;
    private int initcol = 0;
    private int initsmallrow = 0;
    private int initsmallcol = 0;
    private bool _isSolved = false;
    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private bool stuck = false;
    
    //0 = Nor, 1 = Xor, 2 = Or, 3 = And, 4 = Xnor, 5 = Nand
    private int[,] grid = new int[10,10] {
        { 0, 1, 2, 3, 2, 3, 1, 5, 2, 1 },
        { 1, 3, 2, 5, 2, 2, 2, 3, 1, 5 },
        { 2, 4, 2, 2, 1, 0, 2, 3, 2, 4 },
        { 3, 5, 2, 0, 2, 1, 3, 0, 2, 2 },
        { 2, 4, 3, 2, 5, 0, 2, 2, 0, 1 },
        { 1, 2, 5, 0, 2, 2, 3, 0, 1, 2 },
        { 2, 2, 3, 0, 2, 3, 1, 2, 2, 1 },
        { 1, 4, 2, 4, 2, 1, 4, 4, 5, 2 },
        { 1, 2, 2, 2, 5, 4, 0, 5, 2, 1 },
        { 2, 4, 1, 4, 3, 2, 1, 2, 3, 0 }
    };

    private int[,] grid_small = new int[5, 5] {
        { 0, 1, 1, 0, 0 },
        { 0, 1, 0, 0, 1 },
        { 1, 0, 0, 0, 0 },
        { 0, 0, 0, 1, 1 },
        { 1, 1, 0, 0, 1 }
    };

    void Start()
    {
        _moduleId = _moduleIdCounter++;
        char[] serial = BombInfo.GetSerialNumber().ToCharArray();

        //Use 1st/2nd characters of serial to calculate small grid location
        smallgridPosRow = ConvToPosSmall(serial[0]);
        smallgridPosCol = ConvToPosSmall(serial[1]);
        initsmallrow = smallgridPosRow;
        initsmallcol = smallgridPosCol;

        //Use 3rd/4th characters of serial to calculate starting position
        gridPosRow = ConvToPos(serial[2]);
        gridPosCol = ConvToPos(serial[3]);

        //Save starting position for resets
        initrow = gridPosRow;
        initcol = gridPosCol;

        //Use 5th/6th characters of serial to calculate ending position
        correctgridrow = ConvToPos(serial[4]);
        correctgridcol = ConvToPos(serial[5]);

        //Display a random integer between 0 and 3
        booldisplay = Random.Range(0, 4);
        NumDisplay.text = booldisplay + "";

        //Moves ending off of AND/NOR
        CheckBadEnding();

        //Log stuff
        Debug.LogFormat("[Cruel Boolean Maze #{2}] Starting Location: ({0},{1})", initrow, initcol, _moduleId);
        Debug.LogFormat("[Cruel Boolean Maze #{2}] Ending Location: ({0},{1})", correctgridrow, correctgridcol, _moduleId);
        Debug.LogFormat("[Cruel Boolean Maze #{2}] Not Grid Location: ({0},{1})", initsmallrow, initsmallcol, _moduleId);
        Debug.LogFormat("[Cruel Boolean Maze #{1}] Display is {0}", booldisplay, _moduleId);

        //Handles button presses
        ButtonUp.OnInteract += delegate () { HandlePress("u"); return false; };
        ButtonLeft.OnInteract += delegate () { HandlePress("l"); return false; };
        ButtonRight.OnInteract += delegate () { HandlePress("r"); return false; };
        ButtonDown.OnInteract += delegate () { HandlePress("d"); return false; };
        ButtonStuck.OnInteract += delegate () { HandlePress("stuck"); return false; };
        ButtonReset.OnInteract += delegate () { HandlePress("reset"); return false; };
    }

    //Move ending off of AND and NOR
    private void CheckBadEnding()
    {
        while(grid[correctgridrow,correctgridcol] % 3 == 0)
        {
            switch (booldisplay)
            {
                case 0:
                    {
                        correctgridrow--;
                    }
                    break;
                case 1:
                    {
                        correctgridcol++;
                    }
                    break;
                case 2:
                    {
                        correctgridrow++;
                    }
                    break;
                case 3:
                    {
                        correctgridcol--;
                    }
                    break;
            }
            if(correctgridcol > 9 || correctgridcol < 0)
            {
                correctgridcol += (correctgridcol < 0) ? 10 : -10;
            }
            if (correctgridrow > 9 || correctgridrow < 0)
            {
                correctgridrow += (correctgridrow < 0) ? 10 : -10;
            }
        }
    }

    //Returns the position from 0-9 of the letters and numbers of the serial. This is a hacky way of doing it but it works. 
    private int ConvToPos(char serialelement)
    {
        int num = serialelement - '0';
        if(num > 9)
        {
            num += '0' - 'A' + 1;
        }
        while(num > 9)
        {
            num -= 10;
        }
        return num;
    }

    private int ConvToPosSmall(char serialelement)
    {
        int num = serialelement - '0';
        if (num > 9)
        {
            num += '0' - 'A' + 1;
        }
        while (num > 9)
        {
            num -= 10;
        }
        while(num > 4)
        {
            num -= 5;
        }
        return num;
    }

    ///Handles button presses using cases based on the label of each button
    ///Each U,L,D,R button checks to see if the defuser is on the edge of the grid, 
    ///and tries to apply a movement in the direction of the button. If the defuser is on
    ///the edge of the grid, or the defuser attempts to enter an illegal grid position, 
    ///a strike is applied. The user is only reset to their initial position upon illegal
    ///use of the Stuck? button.
    private bool HandlePress(string but)
    {
        if(!_isSolved)
        {
            switch (but)
            {
                case "u":
                    {
                        if(CheckLegalMove(gridPosRow - 1, gridPosCol))
                        {
                            gridPosRow = getCheckIndex(gridPosRow - 1);
                            smallgridPosRow = getCheckIndexSmall(smallgridPosRow - 1);
                            Debug.LogFormat("[Cruel Boolean Maze #{2}] Successfully moved up to ({0},{1})", gridPosRow, gridPosCol, _moduleId);
                            Debug.LogFormat("[Cruel Boolean Maze #{2}] Not Grid moved up to ({0},{1})", smallgridPosRow, smallgridPosCol, _moduleId);
                        }
                        else
                        {
                            BombModule.HandleStrike();
                            Debug.LogFormat("[Cruel Boolean Maze #{2}] Attempted to move up to ({0},{1}) with display {6} but the {5} gate returned 0, strike, Current position ({3},{4})", getCheckIndex(gridPosRow-1), gridPosCol, _moduleId, gridPosRow, gridPosCol, GateCheck(gridPosRow-1,gridPosCol), booldisplay);
                        }
                        ButtonUp.AddInteractionPunch();
                    }
                    break;
                case "l":
                    {
                        if (CheckLegalMove(gridPosRow, gridPosCol - 1))
                        {
                            gridPosCol = getCheckIndex(gridPosCol - 1);
                            smallgridPosCol = getCheckIndexSmall(smallgridPosCol - 1);
                            Debug.LogFormat("[Cruel Boolean Maze #{2}] Successfully moved left to ({0},{1})", gridPosRow, gridPosCol, _moduleId);
                            Debug.LogFormat("[Cruel Boolean Maze #{2}] Not Grid moved left to ({0},{1})", smallgridPosRow, smallgridPosCol, _moduleId);
                        }
                        else
                        {
                            BombModule.HandleStrike();
                            Debug.LogFormat("[Cruel Boolean Maze #{2}] Attempted to move left to ({0},{1}) with display {6} but the {5} gate returned 0, strike, Current position ({3},{4})", gridPosRow, getCheckIndex(gridPosCol-1), _moduleId, gridPosRow, gridPosCol, GateCheck(gridPosRow, gridPosCol - 1), booldisplay);
                        }
                        ButtonLeft.AddInteractionPunch();
                    }
                    break;
                case "r":
                    {
                        if (CheckLegalMove(gridPosRow, gridPosCol + 1))
                        {
                            gridPosCol = getCheckIndex(gridPosCol + 1);
                            smallgridPosCol = getCheckIndexSmall(smallgridPosCol + 1);                    
                            Debug.LogFormat("[Cruel Boolean Maze #{2}] Successfully moved right to ({0},{1})", gridPosRow, gridPosCol, _moduleId);
                            Debug.LogFormat("[Cruel Boolean Maze #{2}] Not Grid moved right to ({0},{1})", smallgridPosRow, smallgridPosCol, _moduleId);
                        }
                        else
                        {
                            BombModule.HandleStrike();
                            Debug.LogFormat("[Cruel Boolean Maze #{2}] Attempted to move right to ({0},{1}) with display {6} but the {5} gate returned 0, strike, Current position ({3},{4})", gridPosRow, getCheckIndex(gridPosCol+1), _moduleId, gridPosRow, gridPosCol, GateCheck(gridPosRow, gridPosCol + 1), booldisplay);
                        }
                        ButtonRight.AddInteractionPunch();
                    }
                    break;
                case "d":
                    {                    
                        if (CheckLegalMove(gridPosRow + 1, gridPosCol))
                        {
                            gridPosRow = getCheckIndex(gridPosRow + 1);
                            smallgridPosRow = getCheckIndexSmall(smallgridPosRow + 1);
                            Debug.LogFormat("[Cruel Boolean Maze #{2}] Successfully moved down to ({0},{1})", gridPosRow, gridPosCol, _moduleId);
                            Debug.LogFormat("[Cruel Boolean Maze #{2}] Not Grid moved down to ({0},{1})", smallgridPosRow, smallgridPosCol, _moduleId);
                        }
                        else
                        {
                            BombModule.HandleStrike();
                            Debug.LogFormat("[Cruel Boolean Maze #{2}] Attempted to move down to ({0},{1}) with display {6} but the {5} gate returned 0, strike, Current position ({3},{4})", getCheckIndex(gridPosRow+1), gridPosCol, _moduleId, gridPosRow, gridPosCol, GateCheck(gridPosRow + 1, gridPosCol), booldisplay);
                        }
                        ButtonDown.AddInteractionPunch();
                    }
                    break;
                case "stuck":
                    {
                        if (!isStuck())
                        {                           
                            BombModule.HandleStrike();
                            Debug.LogFormat("[Cruel Boolean Maze #{2}] Defuser pressed Stuck? at ({0},{1}) but there was a legal move, strike, position reset to ({3},{4}). Not grid reset to ({5},{6}).", gridPosRow, gridPosCol, _moduleId, initrow, initcol, initsmallrow, initsmallcol);

                            //Reset position to initial starting location! 
                            gridPosRow = initrow;
                            gridPosCol = initcol;
                            smallgridPosRow = initsmallrow;
                            smallgridPosCol = initsmallcol;
                        }
                        else
                        {
                            Debug.LogFormat("[Cruel Boolean Maze #{2}] Defuser correctly pressed Stuck? at ({0},{1}) with no legal moves. Display changed.", gridPosRow, gridPosCol, _moduleId);
                            stuck = true; 
                        }
                        ButtonStuck.AddInteractionPunch();
                    }
                    break;
                case "reset":
                    {
                        gridPosRow = initrow;
                        gridPosCol = initcol;
                        smallgridPosRow = initsmallrow;
                        smallgridPosCol = initsmallcol;
                        Debug.LogFormat("[Cruel Boolean Maze #{2}] Defuser pressed Reset! Position reset to ({0},{1}). Not grid reset to ({3},{4}).", initrow, initcol, _moduleId, initsmallrow, initsmallcol);
                        ButtonReset.AddInteractionPunch();
                    }
                    break;
            }

            KMAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, this.transform);

            //Check if the Defuser has reached the goal
            CheckPass();

            //Update Display
            StartCoroutine(UpdateDisplay());

        }

        return false;
    }

    /// Updates the number on the display
    private IEnumerator UpdateDisplay()
    {
        NumDisplay.text = "";
        yield return new WaitForSeconds(0.2f);
        int prevbool = booldisplay;
        if (stuck)
        {
            while (booldisplay == prevbool)
            {
                booldisplay = Random.Range(0, 4);
            }
            stuck = false;
        }
        else
        {
            booldisplay = Random.Range(0, 4);
        }

        NumDisplay.text = booldisplay + "";
        Debug.LogFormat("[Cruel Boolean Maze #{1}] Display updated to {0}", booldisplay, _moduleId);
    }

    /// This function is used to determine if the defuser is completely stuck when pressing the Stuck? button
    /// If this function returns true the user has no legal move and the screen can be updated, otherwise a strike is applied 
    /// and the defuser is reset to their initial position
    private bool isStuck()
    {
        bool leftcheck = false;
        bool rightcheck = false;
        bool upcheck = false;
        bool downcheck = false;
    
        downcheck = !CheckLegalMove(gridPosRow + 1, gridPosCol);    
        upcheck = !CheckLegalMove(gridPosRow - 1, gridPosCol);        
        rightcheck = !CheckLegalMove(gridPosRow, gridPosCol + 1); 
        leftcheck = !CheckLegalMove(gridPosRow, gridPosCol - 1); 
       
        return downcheck && upcheck && leftcheck && rightcheck; //Only true if all legal move checks return false, or you are on the edge
    }

    /// Checks to see if the move the user is trying to apply is legal, based on the logic gate in the square of the grid they attempted to enter
    /// Returns true if the move is legal
    private bool CheckLegalMove(int row, int col)
    {
        row = getCheckIndex(row);
        col = getCheckIndex(col);
        int oper = grid[row, col];
        int smalloper = grid_small[smallgridPosRow, smallgridPosCol];
        bool legal = false;
        
        switch (oper)
        {
            case 0:
                {
                    if (booldisplay == 0) legal = true;
                }
                break; 
            case 1:
                {
                    if (booldisplay == 1 || booldisplay == 2) legal = true;
                }
                break;
            case 2:
                {
                    if (booldisplay != 0) legal = true;
                }
                break;
            case 3:
                {
                    if (booldisplay == 3) legal = true;
                }
                break;
            case 4:
                {
                    if (booldisplay == 0 || booldisplay == 3) legal = true;
                }
                break;
            case 5:
                {
                    if (booldisplay != 3) legal = true; 
                }
                break;
        }
        if (smalloper == 1) legal = (legal == false) ? true : false;
        if (legal) return true;

        return false;
    }

    /// Checks to see if the defuser is at the goal or not
    private bool CheckPass()
    {
        if (gridPosRow == correctgridrow && gridPosCol == correctgridcol)
        {
            Debug.LogFormat("[Cruel Boolean Maze #{0}] Defuser reached the goal. Module solved.", _moduleId);
            BombModule.HandlePass();          
            _isSolved = true;
        }
        return false; 
    }

    //Used for debug log
    private string GateCheck(int row, int col)
    {
        row = getCheckIndex(row);
        col = getCheckIndex(col);
        int gateId = grid[row, col];
        string gateName = "Gate Not Found";
        switch (gateId)
        {
            case 0:
                {
                    gateName = "NOR";
                }
                break;
            case 1:
                {
                    gateName = "XOR";
                }
                break;
            case 2:
                {
                    gateName = "OR";
                }
                break;
            case 3:
                {
                    gateName = "AND";
                }
                break;
            case 4:
                {
                    gateName = "XNOR";
                }
                break;
            case 5:
                {
                    gateName = "NAND";
                }
                break;
        }
        if (grid_small[smallgridPosRow, smallgridPosCol] == 1)
        {
            switch (gateName)
            {
                case "NOR": gateName = "OR"; break;
                case "XOR": gateName = "XNOR"; break;
                case "OR": gateName = "NOR"; break;
                case "AND": gateName = "NAND"; break;
                case "XNOR": gateName = "XOR"; break;
                case "NAND": gateName = "AND"; break;
            }
        }
        return gateName;
    }

    //Check Position used for Loops
    private int getCheckIndex(int index)
    {
        if (index > 9) index = 0;
        if (index < 0) index = 9;
        return index;
    }

    private int getCheckIndexSmall(int index)
    {
        if (index > 4) index = 0;
        if (index < 0) index = 4;
        return index;
    }

    //twitch plays
    private bool inputIsValid(string cmd)
    {
        string[] validstuff = { "u", "up", "d", "down", "l", "left", "r", "right", "reset", "reset!", "stuck", "stuck?" };
        if (validstuff.Contains(cmd.ToLower()))
        {
            return true;
        }
        return false;
    }

    #pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} press <u/d/l/r/reset/stuck> [Presses the specified button]";
    #pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string command)
    {
        string[] parameters = command.Split(' ');
        if (Regex.IsMatch(parameters[0], @"^\s*press\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            if (parameters.Length == 2)
            {
                if (inputIsValid(parameters[1]))
                {
                    yield return null;
                    if (parameters[1].ToLower().Equals("u") || parameters[1].ToLower().Equals("up"))
                    {
                        ButtonUp.OnInteract();
                    }
                    else if (parameters[1].ToLower().Equals("d") || parameters[1].ToLower().Equals("down"))
                    {
                        ButtonDown.OnInteract();
                    }
                    else if (parameters[1].ToLower().Equals("r") || parameters[1].ToLower().Equals("right"))
                    {
                        ButtonRight.OnInteract();
                    }
                    else if (parameters[1].ToLower().Equals("l") || parameters[1].ToLower().Equals("left"))
                    {
                        ButtonLeft.OnInteract();
                    }
                    else if (parameters[1].ToLower().Equals("reset") || parameters[1].ToLower().Equals("reset!"))
                    {
                        ButtonReset.OnInteract();
                    }
                    else if (parameters[1].ToLower().Equals("stuck") || parameters[1].ToLower().Equals("stuck?"))
                    {
                        ButtonStuck.OnInteract();
                    }
                }
            }
            yield break;
        }
    }
}
