﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using StarlightDirector.Entities;
using StarlightDirector.UI.Controls.Primitives;

namespace StarlightDirector.UI.Controls {
    partial class ScoreEditor {

        internal void UpdateBarTexts() {
            var startTime = ScoreBars?.FirstOrDefault()?.Bar?.StartTime ?? 0d;
            foreach (var scoreBar in ScoreBars) {
                scoreBar.UpdateBarIndexText();
                scoreBar.UpdateBarTimeText(TimeSpan.FromSeconds(startTime));
                startTime += scoreBar.Bar.TimeLength;
            }
        }

        protected override void ReloadScore(Score toBeSet) {
            // First, clean up the room before inviting guests. XD (You know where this sentense comes from.)
            // These are presentation layer of the program, just clear them, and let the GC do the rest of the work.
            // Clearing these objects will not affect the underlying model.
            RemoveScoreBars(ScoreBars, false, true);
            LineLayer.NoteRelations.Clear();
            while (SpecialScoreNotes.Count > 0) {
                RemoveSpecialNote(SpecialScoreNotes[0], false);
            }
            if (toBeSet == null) {
                return;
            }

            var temporaryMap = new Dictionary<int, ScoreNote>();
            var allGamingNotes = new List<Note>();
            var noteProcessed = new Dictionary<int, bool>();
            // OK the fun part is here.
            foreach (var bar in toBeSet.Bars) {
                var scoreBar = AddScoreBar(null, false, bar);
                foreach (var note in bar.Notes) {
                    if (!note.IsGamingNote) {
                        continue;
                    }
                    var scoreNote = AddScoreNote(scoreBar, note.IndexInGrid, note.FinishPosition, note);
                    if (scoreNote == null) {
                        Debug.Print("Error creating ScoreNote. Please check project content.");
                    } else {
                        temporaryMap.Add(note.ID, scoreNote);
                        allGamingNotes.Add(note);
                        noteProcessed[note.ID] = false;
                    }
                }
            }

            foreach (var note in allGamingNotes) {
                if (!noteProcessed[note.ID]) {
                    ProcessNoteRelations(note, noteProcessed, temporaryMap, LineLayer.NoteRelations);
                }
            }

            // Variant BPM indicators
            foreach (var note in toBeSet.Notes.Where(n => n.Type == NoteType.VariantBpm)) {
                var specialNote = AddSpecialNote(ScoreBars[note.Bar.Index], note.IndexInGrid, note.Type, note, false);
                specialNote.Note = note;
            }

            UpdateBarTexts();
            RecalcEditorLayout();
            Debug.Print("Done: ScoreEditor.ReloadScore().");
        }

        private static void ProcessNoteRelations(Note root, IDictionary<int, bool> noteProcessed, IDictionary<int, ScoreNote> map, NoteRelationCollection relations) {
            var waitingList = new Queue<Note>();
            waitingList.Enqueue(root);
            while (waitingList.Count > 0) {
                var note = waitingList.Dequeue();
                if (noteProcessed[note.ID]) {
                    continue;
                }
                noteProcessed[note.ID] = true;

                if (note.HasPrevSync) {
                    if (!relations.ContainsPair(map[note.ID], map[note.PrevSyncTarget.ID])) {
                        relations.Add(map[note.ID], map[note.PrevSyncTarget.ID], NoteRelation.Sync);
                        waitingList.Enqueue(note.PrevSyncTarget);
                    }
                }
                if (note.HasNextSync) {
                    if (!relations.ContainsPair(map[note.ID], map[note.NextSyncTarget.ID])) {
                        relations.Add(map[note.ID], map[note.NextSyncTarget.ID], NoteRelation.Sync);
                        waitingList.Enqueue(note.NextSyncTarget);
                    }
                }
                if (note.HasNextFlickOrSlide) {
                    if (!relations.ContainsPair(map[note.ID], map[note.NextFlickOrSlideNote.ID])) {
                        relations.Add(map[note.ID], map[note.NextFlickOrSlideNote.ID], NoteRelation.FlickOrSlide);
                        waitingList.Enqueue(note.NextFlickOrSlideNote);
                    }
                }
                if (note.HasPrevFlickOrSlide) {
                    if (!relations.ContainsPair(map[note.ID], map[note.PrevFlickOrSlideNote.ID])) {
                        relations.Add(map[note.ID], map[note.PrevFlickOrSlideNote.ID], NoteRelation.FlickOrSlide);
                        waitingList.Enqueue(note.PrevFlickOrSlideNote);
                    }
                }
                if (note.IsHoldStart) {
                    if (!relations.ContainsPair(map[note.ID], map[note.HoldTarget.ID])) {
                        relations.Add(map[note.ID], map[note.HoldTarget.ID], NoteRelation.Hold);
                        waitingList.Enqueue(note.HoldTarget);
                    }
                }
            }
        }

        private void OnScoreGlobalSettingsChanged(object sender, EventArgs e) {
            UpdateBarTexts();
        }

        private static readonly double[] TrackCenterXPositions = { 0.2, 0.35, 0.5, 0.65, 0.8 };

    }
}
