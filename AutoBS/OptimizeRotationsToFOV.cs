using AutoBS;//for log statements
using CustomJSONData.CustomBeatmap;
using System;
using System.Collections.Generic;
using System.Linq;
using AutoBS.Patches;
using Microsoft.Identity.Client.Kerberos;//v1.39

public class OptimizeRotationsToFOV
{
    public bool RotationsWereAdjusted = false; // lets us know if FOV had an effect on the map.

    //private EditableCBD eData;
    private List<ERotationEventData> rotations;
    private float timeWindow; // User-defined time window to check for large cumulative rotations
    private int maxRotation; // Maximum rotation angle within half the FOV

    int rotationsReduced = 0; // Counter for rotations reduced in size
    int rotationsRemoved = 0; // Counter for rotations removed

    public OptimizeRotationsToFOV(List<ERotationEventData> rots, float timeWindow, float FOV)//, EditableCBD eData)
    {
        Plugin.LogDebug($"Optimizer FOV: {FOV} Time Window: {timeWindow}");

        //this.eData = eData;
        this.rotations = rots;
        this.timeWindow = timeWindow;
        this.maxRotation = (int)FOV / 2;// / 15; // Convert FOV/2 to 15-degree steps
    }

    public List<ERotationEventData> FOVFix()
    {
        // Work directly with the RotationEvents list in eData

        for (int i = 0; i < rotations.Count; i++)
        {
            float windowStartTime = rotations[i].time;
            var windowRotations = rotations.Where(r => r.time >= windowStartTime && r.time <= windowStartTime + timeWindow).ToList();

            int cumulativeRotation = CalculateCumulativeRotation(windowRotations);

            if (Math.Abs(cumulativeRotation) > maxRotation)
            {
                AdjustRotations(windowRotations, cumulativeRotation, windowStartTime);
            }
        }

        Plugin.LogDebug($"[FOVFix] Optimizer final rotation count: {rotations.Count}. Total Rotations Reduced: {rotationsReduced} Total Rotations Removed: {rotationsRemoved}");

        if (rotationsReduced != 0 || rotationsRemoved != 0)
            RotationsWereAdjusted = true;

        return rotations;
    }

    private int CalculateCumulativeRotation(List<ERotationEventData> windowRotations)
    {
        int sum = 0;
        foreach (var r in windowRotations)
        {
            sum += r.rotation;
        }
        return sum;
    }

    private void AdjustRotations(List<ERotationEventData> windowRotations, int initialCumulativeRotation, float windowStartTime)
    {

        bool isPositiveExcess = initialCumulativeRotation > 0; // Check if excess rotation is positive

        //Decrease rotations step size
        foreach (var rotation in windowRotations)
        {
            int index = rotations.FindIndex(r => r == rotation);

            if (index != -1)//will return -1 if no index found
            {
                // Reduce rotation step by one in the correct direction if not the smallest step
                if ((isPositiveExcess && rotations[index].rotation > 15) || (!isPositiveExcess && rotations[index].rotation < -15))
                {
                    int currentRotation = rotations[index].rotation;

                    rotations[index] = new ERotationEventData(rotations[index].time, rotations[index].rotation + (isPositiveExcess ? -15 : 15));

                    rotationsReduced++; // Increment the counter for reduced rotations

                    //Plugin.Log.Info($"Current Rotation Time: {rotations[index].Item1} Rotation: {currentRotation} New Rotation: {rotations[index].Item2}");

                    // Update windowRotations to reflect changes
                    windowRotations = rotations.Where(r => r.time >= windowStartTime && r.time <= windowStartTime + timeWindow).ToList();

                    // Check if adjustments are sufficient after each modification
                    int newCumulativeRotation = CalculateCumulativeRotation(windowRotations);

                    if (Math.Abs(newCumulativeRotation) <= maxRotation)
                    {
                        //Plugin.Log.Info($"Succeeded in reducing cumulative rotations to: {newCumulativeRotation}. Didn't need to remove any rotations.");
                        return; // Stop adjusting if within limits
                    }
                }
            }
        }

        // Further adjustments if reducing step size was not enough. Remove rotations
        foreach (var rotation in windowRotations)
        {
            int index = rotations.FindIndex(r => r == rotation);

            if (index != -1)
            {
                if ((isPositiveExcess && rotations[index].rotation == 15) || (!isPositiveExcess && rotations[index].rotation == -15))
                {
                    //Plugin.Log.Info($"Removing Rotation: {rotations[index].Item1} Rotation: {rotations[index].Item2}");

                    rotations.RemoveAt(index); // Remove if smallest step in the excess direction

                    rotationsRemoved++; // Increment the counter for removed rotations

                    // Recalculate windowRotations to reflect changes in rotations
                    windowRotations = rotations.Where(r => r.time >= windowStartTime && r.time <= windowStartTime + timeWindow).ToList();

                    // Check if adjustments are sufficient after each removal
                    int newCumulativeRotation = CalculateCumulativeRotation(windowRotations);
                    if (Math.Abs(newCumulativeRotation) <= maxRotation)
                    {
                        //Plugin.Log.Info($"Succeeded in reducing cumulative rotations to: {newCumulativeRotation}. NEEDED to REMOVE rotations to accomplish this task.");
                        return; // Stop adjusting if within limits
                    }
                }
            }
        }
    }
    
}
