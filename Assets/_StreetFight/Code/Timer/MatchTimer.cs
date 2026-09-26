using System;
using System.Collections;
using StreetFight.Code.Abstract;
using TMPro; // swap for UnityEngine.UI.Text if you're not using TextMeshPro
using UnityEngine;

namespace StreetFight.Code.Timer
{
    /// <summary>
    /// Drives the round countdown and, if time runs out before either Fighter is KO'd,
    /// declares whichever Fighter has more remaining Health as the winner.
    /// Drop this on a manager object and assign player1/player2 (hero + the enemy for this round).
    /// </summary>
    public class MatchTimer : MonoBehaviour
    {
        [Header("Fighters")]
        [Tooltip("The two Fighters in this round (hero + the enemy it's currently fighting).")]
        public Fighter player1;
        public Fighter player2;

        [Header("Timer")]
        public float matchDuration = 60f;
        [Tooltip("Text in your center hex UI (or wherever you show the clock).")]
        public TextMeshProUGUI timerText;

        [Header("Round Start Countdown")]
        [Tooltip("Reuses timerText to show 3, 2, 1, then goLabel, before the match clock starts.")]
        public float countdownStepSeconds = 1f;
        public string goLabel = "FIGHT!";
        [Tooltip("How long FIGHT! stays on screen before it clears and the match timer takes over.")]
        public float goHoldSeconds = 0.5f;

        public float TimeRemaining { get; private set; }
        public bool Running { get; private set; }

        /// <summary>Fires only when time runs out and a winner is picked by health. Argument is null on a draw.</summary>
        public event Action<Fighter> TimeUpDecision;

        private void OnEnable()
        {
            Fighter.FightEnded += HandleFightEndedByKO;
        }

        private void OnDisable()
        {
            Fighter.FightEnded -= HandleFightEndedByKO;
        }

        void Start()
        {
            BeginRound();
        }

        private void Update()
        {
            if (!Running) return;

            TimeRemaining -= Time.deltaTime;
            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                Running = false;
                DecideByHealth();
            }
            UpdateUI();
        }

        /// <summary>
        /// Self-contained round start: plays 3-2-1-FIGHT in timerText, then starts the match clock.
        /// Call this once from wherever your round begins (e.g. after fighters spawn/face off).
        /// </summary>
        public void BeginRound()
        {
            StartCoroutine(BeginRoundRoutine());
        }

        private IEnumerator BeginRoundRoutine()
        {
            Running = false;
            Fighter.SetRoundActive(false); // lock input/AI during the countdown

            for (int count = 3; count >= 1; count--)
            {
                if (timerText) timerText.text = count.ToString();
                yield return new WaitForSeconds(countdownStepSeconds);
            }

            if (timerText) timerText.text = goLabel;
            Fighter.SetRoundActive(true); // unlock the moment FIGHT! shows
            yield return new WaitForSeconds(goHoldSeconds);

            StartMatch();
        }

        /// <summary>Starts the match clock immediately, skipping the 3-2-1-FIGHT countdown. Use BeginRound() instead if you want the countdown.</summary>
        public void StartMatch()
        {
            TimeRemaining = matchDuration;
            Running = true;
            UpdateUI();
        }

        public void PauseMatch() => Running = false;
        public void ResumeMatch() => Running = true;

        // A normal KO already sets Fighter.FightOver and fires FightEnded via Die().
        // If that happens before our clock runs out, just stop counting - no health comparison needed.
        private void HandleFightEndedByKO(Fighter loser)
        {
            Running = false;
            Fighter.SetRoundActive(false);
        }

        private void DecideByHealth()
        {
            if (Fighter.FightOver) return; // a KO already decided it this exact frame

            Fighter winner, loser;
            if (player1 == null || player1.IsDead) { winner = player2; loser = player1; }
            else if (player2 == null || player2.IsDead) { winner = player1; loser = player2; }
            else if (Mathf.Approximately(player1.Health, player2.Health)) { winner = null; loser = null; } // draw
            else if (player1.Health > player2.Health) { winner = player1; loser = player2; }
            else { winner = player2; loser = player1; }

            Fighter.SetRoundActive(false);

            // Make the loser go down the same way a real KO would (ragdoll, FightOver, slow-mo, etc.)
            if (loser != null && !loser.IsDead) loser.ForceTimeoutLoss();

            TimeUpDecision?.Invoke(winner);
        }

        private void UpdateUI()
        {
            if (!timerText) return;
            timerText.text = Mathf.CeilToInt(TimeRemaining).ToString();
        }
    }
}