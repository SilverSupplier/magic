using System.Collections;
using System.Collections.Generic;
using MagicExamHall;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MagicExamHall.Tests
{
    public sealed class MagicExamHallSceneSmokeTests
    {
        [UnityTest]
        public IEnumerator SceneLoadsWithCoreDemoObjects()
        {
            SceneManager.LoadScene("MagicExamHall");
            yield return null;
            yield return null;

            var controller = Object.FindFirstObjectByType<ExamGameController>();

            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.StationCount, Is.EqualTo(5));
            Assert.That(Object.FindFirstObjectByType<Canvas>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<EventSystem>(), Is.Not.Null);
            Assert.That(controller.OutputDirectory, Does.Contain("MagicExamHallLogs"));
        }

        [UnityTest]
        public IEnumerator FailedCastEscalatesToFirstHint()
        {
            SceneManager.LoadScene("MagicExamHall");
            yield return null;
            yield return null;

            var controller = Object.FindFirstObjectByType<ExamGameController>();
            Assert.That(controller, Is.Not.Null);

            controller.OpenCurrentStationForTests();
            yield return null;

            Assert.That(controller.IsDrawingPanelVisible, Is.True);
            Assert.That(Object.FindFirstObjectByType<SpellDrawingCanvas>(), Is.Not.Null);
            Assert.That(controller.CurrentAssistLevel, Is.EqualTo(0));

            controller.CastSyntheticForTests(new List<List<StrokeSample>>());
            yield return null;

            Assert.That(controller.IsResultPanelVisible, Is.True);
            Assert.That(controller.CurrentAssistLevel, Is.EqualTo(1));
            Assert.That(controller.LastHintText, Is.Not.Empty);
        }

    }
}
