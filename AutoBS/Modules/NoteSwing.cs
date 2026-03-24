using BeatmapSaveDataVersion2_6_0AndEarlier;
using IPA.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AutoBS.Modules
{
    public class NoteSwing //this holds a single swing!
    {
        /*
        public NoteSwing Clone()
        {
            NoteSwing clonedSwing = new NoteSwing
            {
                SwingDirection = this.SwingDirection,
                IsRemoved = this.IsRemoved,
                RemovalPhase = this.RemovalPhase,
                FirstNoteOfSong = this.FirstNoteOfSong,
                Protected = this.Protected,
                AngleBetweenSwings = this.AngleBetweenSwings,
                PrevSwing = null, // Do not copy references to adjacent swings
                NextSwing = null, // Do not copy references to adjacent swings
            };

            foreach (var note in this._notes)
            {
                clonedSwing.AddNote(note.Clone());
            }

            return clonedSwing;
        }
        */

        public int SwingLength => _notes.Count; // number of notes in a single swing
        public int Angle => (int)SwingDirection.RotationAngle();
        public NoteCutDirection SwingDirection { get; set; } = NoteCutDirection.Any;

        public NoteSwing PrevSwing { get; set; }
        public NoteSwing NextSwing { get; set; }

        //public int PrevAngle { get; set; } = 0; // Angle of prev swing
        //public NoteCutDirection PrevDirection => AdditionalSwingExtensions.DirectionFromAngle(PrevAngle);
        //public int NextAngle { get; set; } = 0; // Angle of next swing
        //public NoteCutDirection NextDirection => AdditionalSwingExtensions.DirectionFromAngle(NextAngle);
        public int AngleBetweenSwings { get; set; } = 0; // Angle between this swing and the next swing

        //public bool   IsBadSwing = false; // set to true after repairing swing still lists it as bad
        public bool IsRemoved = false;
        public string RemovalPhase = "";

        public bool FirstNoteOfSong = false; // use for each color a and b

        public void SetSwingDirection(NoteCutDirection direction)
        {

            foreach (var note in _notes)
            {
                if (note.cutDirection != NoteCutDirection.Any)
                {
                    note.cutDirection = direction;
                    SwingDirection = direction;
                }
            }
        }

        public List<ENoteData> _notes = new List<ENoteData>(); // all the notes in a single swing
        public ENoteData FirstNote => _notes.FirstOrDefault();
        public ColorType ColorType => _notes.Count > 0 ? _notes.First().colorType : ColorType.ColorA;
        //public NoteCutDirection cutDirection => _notes.First().cutDirection;
        public float Time => _notes.Count > 0 ? _notes.First().time : -1f;

        public bool Protected = false;
        public void AddNote(ENoteData note)
        {
            if (_notes.Count == 0 || SwingDirection == NoteCutDirection.Any) // adds a note to the swing and sets the SwingDirection to the 1st note it encounters that is not cutDirection.Any
            {
                if (note.cutDirection != NoteCutDirection.Any)
                {
                    SwingDirection = note.cutDirection;
                }
            }

            _notes.Add(note);
        }
        public IEnumerable<ENoteData> GetNotes()
        {
            return _notes;
        }
        public int GetAngleBetweenSwings(NoteSwing nextSwing) // must be same colorType!!!
        {
            if (SwingDirection == NoteCutDirection.Any || nextSwing == null || nextSwing.SwingDirection == NoteCutDirection.Any)
            {
                AngleBetweenSwings = 180;
                return 180;
            }
            else
            {
                int angleDifference = Math.Abs(Angle - nextSwing.Angle);

                int angleBetween = Math.Min(angleDifference, 360 - angleDifference);

                nextSwing.PrevSwing = this;
                AngleBetweenSwings = angleBetween;

                //if (Time > 113 && Time < 114.2) Plugin.log.Info($"[DiffReducer]  TEST!! {Time:F} Angle: {Angle} {SwingDirection} -- {nextSwing.Time:F} NextAngle: {nextSwing.Angle} {nextSwing.SwingDirection} -- AngleDifference: {angleDifference} AngleBetweenSwings: {angleBetween}");

                // Ensure the angle difference is the smallest possible value - this makes the difference in upLeft(-135) and upRight(135) equal to 90 which is correct
                return angleBetween;
            }
        }
        public int GetAngleBetweenDirections(NoteCutDirection nextDirection) // must be same colorType!!!
        {
            if (SwingDirection == NoteCutDirection.Any || nextDirection == NoteCutDirection.Any)
            {
                return 180;
            }
            else
            {
                int angleDifference = Math.Abs(Angle - nextDirection.RotationAngle());

                int angleBetween = Math.Min(angleDifference, 360 - angleDifference);

                // Ensure the angle difference is the smallest possible value - this makes the difference in upLeft(-135) and upRight(135) equal to 90 which is correct
                return angleBetween;
            }
        }
        public void FullMirrorSwing()
        {
            foreach (var note in _notes)
            {
                MirrorNote(note, 4);
            }
        }
        public void MirrorSwingDirection()
        {
            foreach (var note in _notes)
            {
                note.cutDirection = note.cutDirection.Mirrored();
            }
        }
        public void SwapHandSwing() // not used
        {
            foreach (var note in _notes)
            {
                note.SetProperty("colorType", note.colorType.Opposite());
            }
        }
        public void MirrorNote(ENoteData note, int lineCount = 4)
        {
            note.line = lineCount - 1 - note.line;
            note.rotation = -note.rotation;
            note.colorType = ColorType.Opposite();
            note.cutDirection = note.cutDirection.Mirrored();
            //note.cutDirectionAngleOffset = 0f - cutDirectionAngleOffset;
        }
    }
    static class AdditionalSwingExtensions
    {
        public static bool IsContraryDirectionWithinSwing(this ENoteData note, ENoteData prevNote) // used to determine a note can be part of a swing
        {
            if (note.cutDirection == NoteCutDirection.Any) return false;
            else
            {
                float angleDiff = note.cutDirection.RotationAngle() - prevNote.cutDirection.RotationAngle();
                if (Mathf.Abs(angleDiff) > 45f)
                    return true;
                else
                    return false;
            }
        }
        public static bool IsContraryDirectionBetweenSwings(this NoteCutDirection cutDirection, NoteCutDirection prevCutDirection)
        {
            if (cutDirection == NoteCutDirection.Any) return false;
            else
            {
                float angleDiff = cutDirection.RotationAngle() - prevCutDirection.RotationAngle();
                if (Mathf.Abs(angleDiff) < 90f)
                    return false;
                else
                    return true;
            }
        }
        public static int GetAngleBetweenDirectionsTest(NoteCutDirection currentDirection, NoteCutDirection nextDirection) // must be same colorType!!!
        {
            int currentAngle = currentDirection.RotationAngle();
            int nextAngle = nextDirection.RotationAngle();

            if (currentDirection == NoteCutDirection.Any || nextDirection == NoteCutDirection.Any)
            {
                return 180;
            }
            else
            {
                int angleDifference = Math.Abs(currentAngle - nextAngle);

                // Ensure the angle difference is the smallest possible value - this makes the difference in upLeft(-135) and upRight(135) equal to 90 which is correct
                return Math.Min(angleDifference, 360 - angleDifference);
            }
        }
        public static int GetAngleBetweenAnglesTest(int currentAngle, int nextAngle) // must be same colorType!!!
        {
            int angleDifference = Math.Abs(currentAngle - nextAngle);

            // Ensure the angle difference is the smallest possible value - this makes the difference in upLeft(-135) and upRight(135) equal to 90 which is correct. or 0 and 135 == 45.
            return Math.Min(angleDifference, 360 - angleDifference);

        }

        public static int RotationAngle(this NoteCutDirection cutDirection)
        {
            switch (cutDirection)
            {
                case NoteCutDirection.Left:
                    return 270;
                case NoteCutDirection.Right:
                    return 90;
                case NoteCutDirection.Up:
                    return 0;
                case NoteCutDirection.Down:
                    return 180;
                case NoteCutDirection.UpLeft:
                    return 315;
                case NoteCutDirection.UpRight:
                    return 45;
                case NoteCutDirection.DownLeft:
                    return 225;
                case NoteCutDirection.DownRight:
                    return 135;
                default:
                    return -1; // Any
            }
        }
        public static NoteCutDirection DirectionFromAngle(int angle)
        {
            switch (angle)
            {
                case 270:
                    return NoteCutDirection.Left;
                case 90:
                    return NoteCutDirection.Right;
                case 0:
                    return NoteCutDirection.Up;
                case 180:
                    return NoteCutDirection.Down;
                case 315:
                    return NoteCutDirection.UpLeft;
                case 45:
                    return NoteCutDirection.UpRight;
                case 225:
                    return NoteCutDirection.DownLeft;
                case 135:
                    return NoteCutDirection.DownRight;
                case -1:
                    return NoteCutDirection.Any;
                default:
                    return NoteCutDirection.Any;
            }
        }
        /*
        this is my take on good swing directions:
        any color (colorA left type 0, colorB right type 1)
        2,0; 2,1 up, upleft  2,2; 2,3 up, upright
        1,0; 1,1 up, upleft, left, down, downleft  1,2; 1,3 up, upright, right, down, downright
        0,0; 0,1 up, down, downleft  0,2; 0,3 up, down, downright
        */

        public static bool GoodSwingDirection(NoteSwing swing, NoteCutDirection potentialDirection)
        {
            // Extract the lineLayer and lineIndex from the swing's position
            int lineLayer = (int)swing.FirstNote.layer;
            int lineIndex = swing.FirstNote.line;

            // Check the grid location and assign valid diagonal directions based on your description
            switch (lineLayer)
            {
                case 0:
                    if (lineIndex == 0 || lineIndex == 1) // Bottom left
                    {
                        return potentialDirection == NoteCutDirection.Up ||
                               potentialDirection == NoteCutDirection.Down ||
                               potentialDirection == NoteCutDirection.DownLeft;
                    }
                    else if (lineIndex == 2 || lineIndex == 3) // Bottom right
                    {
                        return potentialDirection == NoteCutDirection.Up ||
                               potentialDirection == NoteCutDirection.Down ||
                               potentialDirection == NoteCutDirection.DownRight;
                    }
                    break;
                case 1:
                    if (lineIndex == 0 || lineIndex == 1) // Middle left
                    {
                        return potentialDirection == NoteCutDirection.Up ||
                               potentialDirection == NoteCutDirection.UpLeft ||
                               potentialDirection == NoteCutDirection.Left ||
                               potentialDirection == NoteCutDirection.Down ||
                               potentialDirection == NoteCutDirection.DownLeft;
                    }
                    else if (lineIndex == 2 || lineIndex == 3) // Middle right
                    {
                        return potentialDirection == NoteCutDirection.Up ||
                               potentialDirection == NoteCutDirection.UpRight ||
                               potentialDirection == NoteCutDirection.Right ||
                               potentialDirection == NoteCutDirection.Down ||
                               potentialDirection == NoteCutDirection.DownRight;
                    }
                    break;
                case 2:
                    if (lineIndex == 0 || lineIndex == 1) // Top left
                    {
                        return potentialDirection == NoteCutDirection.Up ||
                               potentialDirection == NoteCutDirection.UpLeft;
                    }
                    else if (lineIndex == 2 || lineIndex == 3) // Top right
                    {
                        return potentialDirection == NoteCutDirection.Up ||
                               potentialDirection == NoteCutDirection.UpRight;
                    }
                    break;
            }

            // If none of the specific checks passed, the direction is not considered "good"
            return false;
        }

    }
}
