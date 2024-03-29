using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;
using System.ComponentModel;
using System.Diagnostics;

namespace spacedout
{
    interface State
    {
        void Start(StateManager.Control ctrl);
        void Update(long elapsed, Action<string> nextState);
        void Tick(int elapsed) { }
    }

    class Audio
    {
        public static void Play(int id)
        {
            var home = Environment.GetEnvironmentVariable("HOME");
            var path = $"{home}/.local/share/spacedout_audio/{id}.mp3";

            var procInfo = new ProcessStartInfo();
            procInfo.FileName = "/usr/bin/play";
            procInfo.Arguments = "-q " + path;
            Process.Start(procInfo);
        }
    }

    class StateManager : StateManager.Control
    {
        public interface Control
        {
            void nextState(string name);
            void resetTimer();
        }

        Dictionary<string, State> states = new Dictionary<string, State>();
        DateTime lastUpdate = DateTime.Now;
        public State CurrentState;

        public StateManager(int updateFrequency)
        {
            DispatcherTimer.Run(tick, new System.TimeSpan(0, 0, updateFrequency));
            DispatcherTimer.Run(fastTick, new System.TimeSpan(0, 0, 0, 0, 512));
        }

        public void AddState(string name, State state)
        {
            if (states.ContainsKey(name))
            {
                throw new WarningException("Duplicate state key: " + name);
            }
            states.Add(name, state);
        }

        public void Start(string name)
        {
            if (!states.ContainsKey(name))
            {
                throw new WarningException("Unknown state key: " + name);
            }
            var state = states[name];
            CurrentState = state;
            lastUpdate = DateTime.Now;
            state.Start(this);
        }

        public bool tick()
        {
            if (CurrentState != null)
            {
                var now = System.DateTime.Now;
                var elapsed = DateTime.Now.Subtract(lastUpdate).Duration().Seconds;
                CurrentState.Update(elapsed, this.Start);
            }
            return true;
        }
        public bool fastTick()
        {
            if (CurrentState != null)
            {
                var now = System.DateTime.Now;
                var elapsed = DateTime.Now.Subtract(lastUpdate).Duration().Seconds;
                CurrentState.Tick(elapsed);
            }
            return true;
        }

        public void nextState(string name)
        {
            Start(name);
        }

        public void resetTimer()
        {
            lastUpdate = DateTime.Now;
        }
    }

    class ReviewState : State
    {
        MainWindow window;

        List<Phrase> phrases;
        Phrase currentPhrase;
        bool showTranslation = false;

        Translater tr = new Swedish();

        StateManager.Control stateControl;

        Button startQuiz;

        bool blinkQuizButton = false;
        bool isQuizTime = false;

        public ReviewState(MainWindow window)
        {
            this.window = window;
            window.Focusable = false;

            var phraseReveal = window.Find<Button>("phraseReveal");
            var phraseNext = window.Find<Button>("phraseNext");
            var phraseNextAddHour = window.Find<Button>("phraseNextAddHour");
            var phraseNextAddHalfDay = window.Find<Button>("phraseNextAddHalfDay");
            var phraseNextAddDay = window.Find<Button>("phraseNextAddDay");
            startQuiz = window.Find<Button>("startQuiz");
            //var phraseSkip = this.Find<Button>("phraseSkip");

            phraseReveal.Click += onPhraseReveal;
            phraseNext.Click += onPhraseNext;
            phraseNextAddHour.Click += onPhraseNextAddHour;
            phraseNextAddHalfDay.Click += onPhraseNextHalfDay;
            phraseNextAddDay.Click += onPhraseNextDay;
            startQuiz.Click += onStartQuiz;
        }

        public void Start(StateManager.Control ctrl)
        {
            stateControl = ctrl;
            var db = new Db();
            phrases = db.GetPhrases().ToList();
            startQuiz.Background = new SolidColorBrush(0xFF333333);

            if (phrases.Count > 0)
            {
                isQuizTime = false;
                showPhrase(phrases[0]);
            }

        }

        public void showPhrase(Phrase phrase)
        {
            currentPhrase = phrase;

            var freq = window.Find<TextBlock>("frequency");
            var phraseContent = window.Find<TextBlock>("phraseContent");
            var phraseImg = window.Find<Image>("phraseImg");
            phraseContent.Text = string.Format("{0}", phrase.Translation);
            freq.Text = string.Format("({0:0.00})", currentPhrase.Frequency);
            try
            {
                using (var stream = new Db().GetImageStream(phrase))
                {
                    if (stream != null)
                    {
                        phraseImg.Source = new Bitmap(stream);// maybe close?
                    }
                }
            }
            catch (Exception)
            {
                phraseImg.Source = null;
            }
        }


        public void onPhraseReveal(object sender, RoutedEventArgs e)
        {
            if (currentPhrase == null)
            {
                return;
            }
            var phraseContent = window.Find<TextBlock>("phraseContent");
            var freq = window.Find<TextBlock>("frequency");

            showTranslation = !showTranslation;
            phraseContent.Text = string.Format("{0}", !showTranslation ? currentPhrase.Translation : currentPhrase.Text);
            freq.Text = string.Format("({0:0.00})", currentPhrase.Frequency);
            stateControl.resetTimer();

            new Db().IncreaseFrequency(currentPhrase, currentPhrase.Frequency / 4);
        }

        public void onPhraseNext(object sender, RoutedEventArgs e)
        {
            if (currentPhrase != null)
            {
                var db = new Db();
                nextPhrase();
            }
        }

        public void onPhraseNextAddHour(object sender, RoutedEventArgs e)
        {
            if (currentPhrase != null)
            {
                var db = new Db();
                db.DecreaseFrequency(currentPhrase, 60 * 2);
                nextPhrase();
            }
        }

        public void onPhraseNextHalfDay(object sender, RoutedEventArgs e)
        {
            if (currentPhrase != null)
            {
                var db = new Db();
                db.DecreaseFrequency(currentPhrase, 60 * 7);
                nextPhrase();
            }
        }

        public void onPhraseNextDay(object sender, RoutedEventArgs e)
        {
            if (currentPhrase != null)
            {
                var db = new Db();
                db.DecreaseFrequency(currentPhrase, 60 * 24);
                nextPhrase();
            }
        }
        public void onStartQuiz(object sender, RoutedEventArgs e)
        {
            stateControl.nextState("quiz");
        }

        public void nextPhrase()
        {
            var db = new Db();

            if (phrases.Count > 0)
            {
                phrases.RemoveAt(0);
            }

            if (phrases.Count > 0)
            {
                showPhrase(phrases[0]);
            }
            else
            {
                phrases = db.GetPhrases().ToList();
                Phrase phrase;
                if (phrases.Count == 0)
                {
                    phrase = new Phrase { Text = "**" };
                }
                else
                {
                    phrase = phrases[0];
                }
                showPhrase(phrase);
            }
            stateControl.resetTimer();
        }

        public void onCharClick(object sender, RoutedEventArgs e)
        {
            var b = (Button)e.Source;
            var c = (char)b.Content;
            var output = window.Find<TextBox>("charOutput");
            output.Text += c;
            output.Focus();
            output.CaretIndex = output.Text.Length;
        }

        public void Update(long elapsed, Action<string> nextState)
        {
            nextPhrase();
            var db = new Db();
            isQuizTime = db.IsQuizTime();
        }

        public void Tick(int elapsed)
        {
            if (!isQuizTime)
            {
                return;
            }
            if (blinkQuizButton)
            {
                startQuiz.Background = new SolidColorBrush(0xFFFF0000);
            }
            else
            {
                startQuiz.Background = new SolidColorBrush(0xFFFF5500);
            }
            blinkQuizButton = !blinkQuizButton;
        }
    }


    // TODO:
    class QuizState : State
    {

        MainWindow mainWindow;
        QuizWindow quizWindow;

        List<Phrase> phrases;
        List<Phrase> repeatPhrases;

        int phraseIndex;

        enum SubState { Foobar, Reveal, InputAnswer }

        SubState subState = SubState.Foobar;

        StateManager.Control stateControl;

        int numPhrases = 15;

        bool repeatRound = false;

        List<string> correctWordList = new List<string>();
        List<string> answerWordList = new List<string>();

        public QuizState(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;

            var b = mainWindow.Screens.Primary.Bounds;
            quizWindow = new QuizWindow();
            quizWindow.WindowStartupLocation = WindowStartupLocation.Manual;
            quizWindow.Position = new PixelPoint(0, 0);
            quizWindow.Width = b.Width;
            quizWindow.Height = b.Height;
            repeatPhrases = new List<Phrase>();

            var translation = quizWindow.Find<TextBlock>("translation");
            var text = quizWindow.Find<TextBlock>("text");
            var inputTranslation = quizWindow.Find<TextBox>("inputTranslation");

            getButton("play").Click += (s, e) => play();
            getButton("nope").Click += onNope;
            getButton("okay").Click += onOkay;
            getButton("reveal").Click += (s, e) => reveal();
            getButton("backspace").Click += onBackspace;
            inputTranslation.KeyUp += onInput;
        }
        public void Start(StateManager.Control ctrl)
        {
            stateControl = ctrl;
            quizWindow.Show();
            quizWindow.Title = "__spacedout_quiz";
            phrases = new Db().GetPhrases(numPhrases).ToList();
            repeatPhrases.Clear();
            repeatRound = false;

            if (phrases.Count == 0)
            {
                quizWindow.Hide();
                stateControl.nextState("review");
                return;
            }

            phraseIndex = -1;
            nextPhrase();
        }
        public void Update(long elapsed, Action<string> nextState)
        {

        }

        public void Tick(int elapsed)
        {
            switch (subState)
            {
                case SubState.Foobar:
                    var text = quizWindow.Find<TextBlock>("text");
                    if (elapsed >= 4 && !text.IsVisible)
                    {
                        showButton("reveal");
                    }
                    break;
                case SubState.InputAnswer:
                    if (elapsed >= 5)
                    {
                        showButton("play");
                    }
                    break;
                case SubState.Reveal:
                    if (elapsed >= 2)
                    {
                        //nextPhrase
                    }
                    break;
            }
        }

        void play()
        {
            var phrase = phrases[phraseIndex];
            Audio.Play(phrase.ID);
        }

        void onNope(object sender, RoutedEventArgs e)
        {
            var phrase = phrases[phraseIndex];
            new Db().IncreaseFrequency(phrase, phrase.Frequency / 3);
            if (phraseIndex < numPhrases - 2)
            {
                repeatPhrases.Add(phrase);
            }
            nextPhrase();
        }

        void onOkay(object sender, RoutedEventArgs e)
        {
            var phrase = phrases[phraseIndex];

            var n = repeatRound ? 4 : 2;
            new Db().DecreaseFrequency(phrase, phrase.Frequency / n);
            nextPhrase();
        }

        void onBackspace(object sender, RoutedEventArgs e)
        {
            if (answerWordList.Count == 0)
            {
                return;
            }
            answerWordList.RemoveAt(answerWordList.Count - 1);
            var translation = quizWindow.Find<TextBlock>("translation");
            translation.Text = "";
            foreach (var w in answerWordList)
            {
                translation.Text += w + " ";
            }
        }

        void onInput(object sender, Avalonia.Input.KeyEventArgs e)
        {
            var phrase = phrases[phraseIndex];
            var inputTranslation = quizWindow.Find<TextBox>("inputTranslation");

            if (inputTranslation.Text.ToLower() != phrase.Translation.ToLower())
            {
                inputTranslation.Foreground = new SolidColorBrush(0xFF006600);
            }
            else
            {
                inputTranslation.Foreground = new SolidColorBrush(0xFF660000);
            }
        }

        bool isCorrectAnswer()
        {
            if (correctWordList.Count != answerWordList.Count)
            {
                return false;
            }

            var i = 0;
            foreach (var w in correctWordList)
            {
                if (answerWordList[i] != w)
                {
                    return false;
                }
                i++;
            }
            return true;
        }


        void reveal()
        {
            var phrase = phrases[phraseIndex];
            if (phrase.IsAged())
            {
                if (isCorrectAnswer())
                {
                    showButton("okay");
                }
                else
                {
                    showButton("nope");
                }
                hideButton("reveal");
                hideButton("backspace");
                showButton("play");
                Audio.Play(phrase.ID);
                var text = quizWindow.Find<TextBlock>("text");
                text.Text = phrase.Translation;
            }
            else
            {
                var text = quizWindow.Find<TextBlock>("text");
                text.IsVisible = true;
                showButton("nope");
                showButton("okay");
                hideButton("reveal");
                subState = SubState.Foobar;

                play();
            }
        }

        // TODO
        // if wrong for the first time, then show audio btn and play audio
        // if phrase is really aged, then play only audio, do not show translation

        void endQuiz()
        {
            quizWindow.Hide();
            new Db().UpdateQuizTime();
            stateControl.nextState("review");
        }

        bool nextPhrase()
        {
            phraseIndex++;

            answerWordList.Clear();
            var shuffledButtons = quizWindow.Find<WrapPanel>("shuffledButtons");
            shuffledButtons.Children.Clear();

            if (phraseIndex >= phrases.Count)
            {
                if (!repeatRound && repeatPhrases.Count > 0)
                {
                    phraseIndex = 0;
                    phrases.Clear();
                    phrases.AddRange(repeatPhrases);
                    repeatPhrases.Clear();
                    repeatRound = true;
                }
                else
                {
                    endQuiz();
                    return false;
                }
            }

            var rnd = new Random();
            var phrase = phrases[phraseIndex];
            if (phrase.IsAged())
            {
                foobar();
                var text = quizWindow.Find<TextBlock>("text");
                var translation = quizWindow.Find<TextBlock>("translation");
                text.IsVisible = true;
                text.Text = phrase.Text;
                translation.Text = "";
                showButton("backspace");
                showButton("reveal");
                hideButton("play");
                hideButton("nope");
                hideButton("okay");
            }
            else
            {
                var translation = quizWindow.Find<TextBlock>("translation");
                var text = quizWindow.Find<TextBlock>("text");
                translation.Text = string.Format("{0}", phrase.Translation);
                text.Text = string.Format("{0}", phrase.Text);
                text.IsVisible = false;
                subState = SubState.Foobar;
                hideButton("backspace");
                hideButton("nope");
                hideButton("okay");
                hideButton("reveal");
                stateControl.resetTimer();
                Audio.Play(phrase.ID);
            }

            return true;
        }

        void foobar()
        {
            var phrase = phrases[phraseIndex];

            correctWordList = removeNotation(phrase.Translation)
                .Split(" ")
                .Where(p => p.Length > 0)
                .ToList();

            var rnd = new Random();
            var otherWords = new Db().GetRandomPhrases(3)
            .Concat(new Phrase[] { phrase })
            .SelectMany(p =>
            {
                return removeNotation(p.Translation)
                .Split(" ")
                .Where(p => p.Length > 0)
                .ToList();
            }).ToHashSet().ToList().OrderBy(item => item);

            var translation = quizWindow.Find<TextBlock>("translation");
            var shuffledButtons = quizWindow.Find<WrapPanel>("shuffledButtons");
            foreach (var w in otherWords)
            {
                var btn = new Button();
                btn.Content = w;
                btn.FontSize = 21;
                btn.MinWidth = 140;
                btn.Click += (s, e) =>
                {
                    answerWordList.Add(w);
                    translation.Text += w + " ";
                };
                shuffledButtons.Children.Add(btn);
            }
        }

        string removeNotation(string s)
        {
            return s.ToLower()
                .Replace(".", "")
                .Replace(",", "")
                .Replace("!", "")
                .Replace("?", "");
        }

        Button getButton(string name)
        {
            return quizWindow.Find<Button>(name);
        }
        void hideButton(string name)
        {
            quizWindow.Find<Button>(name).IsVisible = false;
        }
        void showButton(string name)
        {
            quizWindow.Find<Button>(name).IsVisible = true;
        }

        void showInput()
        {
            quizWindow.Find<TextBox>("inputTranslation").IsVisible = true;
        }
        void hideInput()
        {
            quizWindow.Find<TextBox>("inputTranslation").IsVisible = false;
        }
    }


    public class MainWindow : Window
    {
        IBrush savedBackground;

        Translater tr = new Swedish();


        StateManager stateManager;

        public MainWindow()
        {
            InitializeComponent();
            var b = Screens.Primary.Bounds;
            Width = b.Width;
            this.Focusable = false;

            var container = this.Find<DockPanel>("container");

            Closing += onClose;
            Opened += onOpened;
            Activated += onActivated;
            Deactivated += onDeactivated;


            //Background = new SolidColorBrush(0xAA222222);
            //Position = PixelPoint.FromPoint(new Point(0, 0), 0);
            //WindowStartupLocation = WindowStartupLocation.Manual;
            //CanResize = false;

            //var charsPanel = this.Find<WrapPanel>("chars");
            //foreach (var c in tr.chars())
            //{

            //    var b = new Button();
            //    b.Content = c;
            //    charsPanel.Children.Add(b);
            //    b.Click += this.onCharClick;
            //}

            DispatcherTimer.Run(tick, new System.TimeSpan(0, 0, 1));

            stateManager = new StateManager(10);
            stateManager.AddState("review", new ReviewState(this));
            stateManager.AddState("quiz", new QuizState(this));
            stateManager.Start("review");


        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public void onActivated(object sender, EventArgs e)
        {
            Background = new SolidColorBrush(0xFF660000);
        }
        public void onDeactivated(object sender, EventArgs e)
        {
            Background = savedBackground;
        }

        public void onOpened(object sender, EventArgs e)
        {
            var b = Screens.Primary.Bounds;
            //Width = b.Width;
            Position = new PixelPoint(0, b.Bottom - (int)Height);

            HasSystemDecorations = false;
            ShowInTaskbar = false;
            Topmost = true;
            savedBackground = Background;

        }
        public void onFocus(object sender, RoutedEventArgs e)
        {
        }

        public void onBlur(object sender, RoutedEventArgs e)
        {
        }

        public void onClose(object sender, CancelEventArgs e)
        {
            e.Cancel = true;
        }
        public bool tick()
        {
            var date = this.Find<TextBlock>("date");
            var time = this.Find<TextBlock>("time");
            var weekDay = this.Find<TextBlock>("weekDay");
            var month = this.Find<TextBlock>("month");
            var now = System.DateTime.Now;
            time.Text = tr.word("time") + ": " + tr.time(now);
            //weekDay.Text = tr.word("weekday") + ": " + tr.weekDay(now.DayOfWeek);
            //month.Text = tr.word("month") + ": " + tr.month(now.Month);
            date.Text = string.Format("{0}, {2} {1}", tr.weekDay(now.DayOfWeek), tr.number(now.Day), tr.month(now.Month));

            return true;
        }
    }

    class QuizWindow : Window
    {
        public QuizWindow()
        {
            InitializeComponent();
        }
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}