﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using DereTore.Common;
using StarlightDirector.Entities;
using StarlightDirector.Entities.Extensions;
using StarlightDirector.Extensions;
using StarlightDirector.UI.Controls.Primitives;

namespace StarlightDirector.UI.Controls {
    partial class ScoreEditor {

        private void BarLayer_OnSizeChanged(object sender, SizeChangedEventArgs e) {
            ResizeBars();
            RepositionBars();
        }

        private void NoteLayer_OnSizeChanged(object sender, SizeChangedEventArgs e) {
            RepositionNotes();
            // We have to be sure the lines reposition after the notes did.
            RepositionLineLayer();
        }

        private void ScoreBar_MouseUp(object sender, MouseButtonEventArgs e) {
            var scoreBar = (ScoreBar)sender;
            var hitTestInfo = scoreBar.HitTest(e.GetPosition(scoreBar));
            if (e.ChangedButton == MouseButton.Left) {
                if (hitTestInfo.IsValid) {
                    var scoreNote = AddScoreNote(scoreBar, hitTestInfo, null);
                    if (scoreNote != null) {
                        var note = scoreNote.Note;
                        if (note.IsSync) {
                            ScoreNote prevScoreNote = null;
                            ScoreNote nextScoreNote = null;
                            if (note.HasPrevSync) {
                                var prevNote = note.PrevSyncTarget;
                                prevScoreNote = FindScoreNote(prevNote);
                                LineLayer.NoteRelations.Add(scoreNote, prevScoreNote, NoteRelation.Sync);
                            }
                            if (note.HasNextSync) {
                                var nextNote = note.NextSyncTarget;
                                nextScoreNote = FindScoreNote(nextNote);
                                LineLayer.NoteRelations.Add(scoreNote, nextScoreNote, NoteRelation.Sync);
                            }
                            if (note.HasPrevSync && note.HasNextSync) {
                                LineLayer.NoteRelations.Remove(prevScoreNote, nextScoreNote);
                            }
                            LineLayer.InvalidateVisual();
                        }
                    }
                } else {
                    UnselectAllScoreNotes();
                    SelectScoreBar(scoreBar);
                }
                e.Handled = true;
            } else {
                if (HasSelectedScoreNotes) {
                    UnselectAllScoreNotes();
                    e.Handled = true;
                }
            }
        }

        private void ScoreBar_MouseDown(object sender, MouseButtonEventArgs e) {
            e.Handled = true;
        }

        private void ScoreNote_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton != MouseButton.Left) {
                e.Handled = true;
                return;
            }
            var scoreNote = (ScoreNote)sender;
            var isControlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
            if (isControlPressed) {
                scoreNote.IsSelected = !scoreNote.IsSelected;
            } else {
                var originalSelected = scoreNote.IsSelected;
                var originalMoreThanOneSelectedNotes = GetSelectedScoreNotes().CountMoreThan(1);
                UnselectAllScoreNotes();
                // !originalSelected || (originalSelected && originalAnySelectedNotes)
                if (!originalSelected || originalMoreThanOneSelectedNotes) {
                    scoreNote.IsSelected = true;
                }
            }
            if (scoreNote.IsSelected && EditMode != EditMode.ResetNote) {
                switch (EditMode) {
                    case EditMode.CreateRelations:
                        EditingLine.Stroke = LineLayer.RelationBrush;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(EditMode));
                }
                EditingLine.X1 = EditingLine.X2 = scoreNote.X;
                EditingLine.Y1 = EditingLine.Y2 = scoreNote.Y;
                EditingLine.Visibility = Visibility.Visible;
            }
            var note = scoreNote.Note;
            var barIndex = note.Bar.Index;
            var row = note.IndexInGrid;
            var column = note.IndexInTrack;
            Debug.Print($"Note @ bar#{barIndex}, row={row}, column={column}");
            DraggingStartNote = scoreNote;
            // Prevent broadcasting this event to ScoreEditor.
            e.Handled = true;
        }

        private void ScoreNote_MouseUp(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton != MouseButton.Left) {
                UnselectAllScoreNotes();
                e.Handled = true;
                return;
            }
            DraggingEndNote = sender as ScoreNote;
            Debug.Assert(DraggingEndNote != null, "DraggingEndNote != null");
            if (DraggingStartNote != null && DraggingEndNote != null) {
                var mode = EditMode;
                var start = DraggingStartNote;
                var end = DraggingEndNote;
                var ns = start.Note;
                var ne = end.Note;
                if (mode == EditMode.ResetNote) {
                    ns.ResetConnection();
                    LineLayer.NoteRelations.RemoveAllConnections(start);
                    if (!start.Equals(end)) {
                        ne.ResetConnection();
                        LineLayer.NoteRelations.RemoveAllConnections(end);
                    }
                    LineLayer.InvalidateVisual();
                    Project.IsChanged = true;
                } else if (!start.Equals(end)) {
                    if (LineLayer.NoteRelations.ContainsPair(start, end)) {
                        MessageBox.Show(Application.Current.FindResource<string>(App.ResourceKeys.NoteRelationAlreadyExistsPrompt), App.Title, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        return;
                    }
                    if (mode != EditMode.CreateRelations) {
                        throw new ArgumentOutOfRangeException(nameof(mode));
                    }
                    var first = ns < ne ? ns : ne;
                    var second = first.Equals(ns) ? ne : ns;
                    if (ns.Bar == ne.Bar && ns.IndexInGrid == ne.IndexInGrid && !ns.IsSync && !ne.IsSync) {
                        // sync
                        Note.ConnectSync(first, second);
                        LineLayer.NoteRelations.Add(start, end, NoteRelation.Sync);
                        LineLayer.InvalidateVisual();
                    } else if (ns.FinishPosition != ne.FinishPosition && (ns.Bar != ne.Bar || ns.IndexInGrid != ne.IndexInGrid) && (!ns.IsHoldStart && !ne.IsHoldStart) && (first.IsSlide == second.IsSlide)) {
                        // flick
                        if (first.HasNextConnect || second.HasPrevConnect) {
                            MessageBox.Show(Application.Current.FindResource<string>(App.ResourceKeys.FlickRelationIsFullPrompt), App.Title, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                            return;
                        }
                        Note.ConnectFlick(first, second);
                        LineLayer.NoteRelations.Add(start, end, NoteRelation.Flick);
                        LineLayer.InvalidateVisual();
                    } else if (ns.FinishPosition == ne.FinishPosition && !ns.IsHold && !ne.IsHold && !first.IsFlick && !first.IsSlide && !second.IsSlide) {
                        // hold
                        var anyObstacles = Score.Notes.AnyNoteBetween(ns, ne);
                        if (anyObstacles) {
                            MessageBox.Show(Application.Current.FindResource<string>(App.ResourceKeys.InvalidHoldCreationPrompt), App.Title, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                            return;
                        }
                        Note.ConnectHold(first, second);
                        LineLayer.NoteRelations.Add(start, end, NoteRelation.Hold);
                        LineLayer.InvalidateVisual();
                    } else {
                        DraggingStartNote = DraggingEndNote = null;
                        e.Handled = true;
                        return;
                    }

                    Project.IsChanged = true;
                    end.IsSelected = true;
                }
            }
            DraggingStartNote = DraggingEndNote = null;
            e.Handled = true;
        }

        private void ScoreNote_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton != MouseButton.Left) {
                e.Handled = true;
                return;
            }
            var scoreNote = (ScoreNote)sender;
            var note = scoreNote.Note;
            if (note.IsHoldEnd || note.IsSlideEnd) {
                switch (note.FlickType) {
                    case NoteFlickType.Tap:
                        note.FlickType = NoteFlickType.FlickLeft;
                        Project.IsChanged = true;
                        break;
                    case NoteFlickType.FlickLeft:
                        note.FlickType = NoteFlickType.FlickRight;
                        Project.IsChanged = true;
                        break;
                    case NoteFlickType.FlickRight:
                        note.FlickType = NoteFlickType.Tap;
                        Project.IsChanged = true;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(note.FlickType));
                }
            }
            e.Handled = true;
        }

        private void ScoreNote_MouseEnter(object sender, MouseEventArgs e) {
            var block = NoteInfoBlock;
            if (block == null) {
                return;
            }
            var scoreNote = (ScoreNote)sender;
            var note = scoreNote.Note;

            block.Inlines.Add($"ID: {note.ID}");
            block.Inlines.Add(new LineBreak());
            block.Inlines.Add($"Timing: {note.HitTiming:0.000000}s");

            string noteTypeString;
            if (note.IsTap) {
                noteTypeString = "Tap";
            } else if (note.IsFlick) {
                noteTypeString = "Flick";
                if (note.IsHold) {
                    noteTypeString += " (hold end)";
                } else if (note.IsSlide) {
                    noteTypeString += " (slide end)";
                }
            } else if (note.IsHold) {
                noteTypeString = "Hold";
            } else if (note.IsSlide) {
                noteTypeString = "Slide";
                if (note.IsSlideStart) {
                    noteTypeString += " (start)";
                } else if (note.IsSlideContinuation) {
                    noteTypeString += " (continued)";
                } else if (note.IsSlideEnd) {
                    noteTypeString += " (end)";
                }
            } else {
                noteTypeString = "#ERR";
            }
            string noteExtra = null;
            if (note.IsSync || note.IsHoldEnd) {
                var syncStr = note.IsSync ? "sync" : null;
                var holdEndStr = note.IsHoldEnd ? "hold-end" : null;
                var extras = new[] { syncStr, holdEndStr };
                noteExtra = extras.Aggregate((prev, val) => {
                    if (string.IsNullOrEmpty(prev)) {
                        return val;
                    }
                    if (string.IsNullOrEmpty(val)) {
                        return prev;
                    }
                    return prev + ", " + val;
                });
            }
            var flickStr = note.FlickType != NoteFlickType.Tap ? (note.FlickType == NoteFlickType.FlickLeft ? "left" : "right") : null;
            block.Inlines.Add(new LineBreak());
            block.Inlines.Add($"Type: {noteTypeString}");
            if (!string.IsNullOrEmpty(noteExtra)) {
                block.Inlines.Add(new LineBreak());
                block.Inlines.Add(noteExtra);
            }
            if (!string.IsNullOrEmpty(flickStr)) {
                block.Inlines.Add(new LineBreak());
                block.Inlines.Add($"Flick: {flickStr}");
            }

            block.Inlines.Add(new LineBreak());
            block.Inlines.Add($"Start: {(int)note.StartPosition}");
            block.Inlines.Add(new LineBreak());
            block.Inlines.Add($"Finish: {(int)note.FinishPosition}");
        }

        private void ScoreNote_MouseLeave(object sender, MouseEventArgs e) {
            NoteInfoBlock?.Inlines.Clear();
        }

        private void ScoreEditor_OnMouseDown(object sender, MouseButtonEventArgs e) {
            UnselectAllScoreNotes();
            UnselectAllScoreBars();
        }

        private void ScoreEditor_OnMouseUp(object sender, MouseButtonEventArgs e) {
            DraggingStartNote = DraggingEndNote = null;
            if (e.ChangedButton == MouseButton.Right) {
                var myPosition = e.GetPosition(this);
                var result = VisualTreeHelper.HitTest(this, myPosition);
                var element = result.VisualHit as FrameworkElement;
                element = element?.FindVisualParent<ScoreBar>();
                if (element != null) {
                    var hitTestInfo = ((ScoreBar)element).HitTest(e.GetPosition(element));
                    LastHitTestInfo = hitTestInfo;
                } else {
                    var s = (from scoreBar in ScoreBars
                                  let top = Canvas.GetTop(scoreBar)
                                  let bottom = top + scoreBar.ActualHeight
                                  where top <= myPosition.Y && myPosition.Y < bottom
                                  select scoreBar)
                                  .FirstOrDefault();
                    if (s != null) {
                        var hitTestInfo = s.HitTest(e.GetPosition(s));
                        LastHitTestInfo = hitTestInfo;
                    }
                }
            }
        }

        private void ScoreEditor_OnPreviewMouseDown(object sender, MouseButtonEventArgs e) {
            Focus();
        }

        private void ScoreEditor_OnPreviewMouseUp(object sender, MouseButtonEventArgs e) {
            EditingLine.Visibility = Visibility.Hidden;
        }

        private void ScoreEditor_OnPreviewMouseMove(object sender, MouseEventArgs e) {
            if (EditingLine.Visibility == Visibility.Visible) {
                var position = e.GetPosition(EditingLineLayer);
                EditingLine.X2 = position.X;
                EditingLine.Y2 = position.Y;
            }
        }

    }
}
