// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

public class MovingAverageFloat
{
    private float[] window;
    private float total;
    private int numSamples;
    private int insertionIndex;
    private int period;

    public MovingAverageFloat(int period)
    {
        this.period = period;
        window = new float[period];
        Clear();
    }

    public void AddSample(float sample)
    {
        // Advance the insertion index.
        if (this.numSamples != 0)
        {
            this.insertionIndex++;
            if (this.insertionIndex == this.period)
            {
                this.insertionIndex = 0;
            }
        }

        if (this.numSamples < period)
        {
            this.numSamples++;
        }
        else
        {
            this.total -= this.window[this.insertionIndex];
        }

        this.window[this.insertionIndex] = sample;
        this.total += sample;
    }

    public void Clear()
    {
        this.total = 0;
        this.numSamples = 0;
        this.insertionIndex = 0;
    }

    public bool HasSamples()
    {
        return this.numSamples != 0;
    }

    public float Total
    {
        get { return this.total; }
    }

    public float Average
    {
        get
        {
            return this.numSamples > 0
                ? (this.total / this.numSamples)
                : 0;
        }
    }

    public float StandardDeviation
    {
        get
        {
            if (this.numSamples == 0)
            {
                return 0.0f;
            }

            float average = Average;
            float squaredDifferences = 0;
            foreach (float val in this.window)
            {
                var difference = val - Average;
                squaredDifferences += (difference * difference);
            }

            float squaredStd = squaredDifferences / this.numSamples;
            float std = Mathf.Sqrt(squaredStd);
            return std;
        }
    }

    public int NumSamples
    {
        get { return this.numSamples; }
    }

    public float LastSample
    {
        get { return numSamples > 0 ? this.window[this.insertionIndex] : 0; }
    }
};
