using System.Linq;

namespace WordleRobot
{
    public partial class Form1 : Form
    {
        public enum SlotType
        {
            Unknown = -1,
            Green = 0, //The letter is in the word and in the correct spot 
            Yellow = 1, //The letter is in the word but in the wrong spot
            Gray = 2, //The letter is not in the target word at all
        }

        public Form1()
        {
            InitializeComponent();
        }

        private WindowsInput.InputSimulator input;

        private void btnStart_Click(object sender, EventArgs e)
        {
            using (var overlay = new SelectionOverlay("Draw a rectangle around the game area to start"))
            {
                if (overlay.ShowDialog() == DialogResult.OK)
                {
                    var language = cmbLanguage.Text;
                    var green = txtGreen.Text;
                    var yellow = txtYellow.Text;
                    var gray = txtGray.Text;

                    if (string.IsNullOrEmpty(language))
                        language = "english";

                    btnStart.Enabled = false;


                    var task = new Task(() =>
                    {
                        var random = new Random();
                        var dictionary = ReadDictionary($"{language.ToLower()}.txt");
                        var goodLetters = new HashSet<char>();
                        var badLetters = new HashSet<char>();

                        input.Keyboard.Sleep(500);

                        //Click to set the focus to the window where the game is
                        input.Mouse.LeftButtonClick();

                        var currentWord = "";
                        var colors = new SlotType[] { };

                        for (var guess = 0; guess < 6; guess++)
                        {

                            var possibleNextWords = guess == 0 
                                ? dictionary
                                : FindPossibleWords(currentWord, dictionary, colors, goodLetters, badLetters);

                            while (true) {

                                if (possibleNextWords.Count == 0) {
                                    break;
                                }

                                currentWord = possibleNextWords[random.Next(possibleNextWords.Count)];

                                input.Keyboard.TextEntry(currentWord);
                                input.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.RETURN);

                                input.Keyboard.Sleep(3000);

                                colors = DetectSlotTypes(guess, overlay.GetSelectionArea(), green, yellow, gray);

                                possibleNextWords.Remove(currentWord);

                                if (colors.Any(x => x == SlotType.Unknown)) //the word is not accepted
                                {
                                    input.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.BACK);
                                    input.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.BACK);
                                    input.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.BACK);
                                    input.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.BACK);
                                    input.Keyboard.KeyPress(WindowsInput.Native.VirtualKeyCode.BACK);
                                }
                                else
                                {
                                    for (var i = 0; i < colors.Length; i++)
                                    {
                                        if (colors[i] == SlotType.Yellow || colors[i] == SlotType.Green)
                                            goodLetters.Add(currentWord[i]);
                                        else
                                            badLetters.Add(currentWord[i]);
                                    }
                                    badLetters = badLetters.Except(goodLetters).ToHashSet();
                                    break;
                                }
                            }
 
                            input.Keyboard.Sleep(2000);
                        }

                    });

                    task.Start();
                }
            }
        }

        private List<string> FindPossibleWords(string currentWord, List<string> dictionary, SlotType[] colors, HashSet<char> goodLetters, HashSet<char> badLetters)
        {
            var possibleWords = new List<string>();

            foreach(var word in dictionary)
            {
                if (IsAPossibleWord(word, currentWord, colors, goodLetters, badLetters))
                    possibleWords.Add(word);
            }

            return possibleWords;
        }

        private bool IsAPossibleWord(string word, string currentWord, SlotType[] colors, HashSet<char> goodLetters, HashSet<char> badLetters)
        {
            if (badLetters.Count > 0 && badLetters.Any(c => word.Contains(c))) return false;
            if (goodLetters.Count > 0 && !goodLetters.All(c => word.Contains(c))) return false;

            for (var i = 0; i < colors.Length; i++)
            {
                if (colors[i] == SlotType.Gray && word[i] == currentWord[i])
                {
                    return false;
                }
                if (colors[i] == SlotType.Green && word[i] != currentWord[i])
                {
                    return false;
                }
                if (colors[i] == SlotType.Yellow && word[i] == currentWord[i])
                {
                    return false;
                }
            }

            return true;
        }

        private SlotType[] DetectSlotTypes(int rowIndex, Rectangle gameArea, string green, string yellow, string gray)
        {
            var slotTypes = new SlotType[5];

            var rowHeight = gameArea.Height / 6;
            var rowWidth = gameArea.Width / 5;

            var colors = new List<string> { green.ToLower().Replace("#", ""), yellow.ToLower().Replace("#", ""), gray.ToLower().Replace("#", "") };

            var screenshot = TakeScreenshot(new Rectangle(gameArea.X, gameArea.Y + (rowHeight * rowIndex), gameArea.Width, rowHeight));

            for (var i=0; i < slotTypes.Length; i++)
            {
                var x = (i * rowWidth) + (rowWidth / 2);

                var samplePixel = screenshot.GetPixel(x, 10);
                var hex = $"{samplePixel.R.ToString("X")}{samplePixel.G.ToString("X")}{samplePixel.B.ToString("X")}";

                slotTypes[i] = (SlotType)colors.IndexOf(hex.ToLower());
                
            }

            return slotTypes;
        }

        private Bitmap TakeScreenshot(Rectangle rectangle, float leftPaddingPc = 0, float rightPaddingPc = 0, float topPaddingPc = 0, float bottomPaddingPc = 0)
        {

            int leftPadding = (int)((float)rectangle.Width * leftPaddingPc);
            var topPadding = (int)((float)rectangle.Height * topPaddingPc);
            int rightPadding = (int)((float)rectangle.Width * rightPaddingPc);
            var bottomPadding = (int)((float)rectangle.Height * bottomPaddingPc);

            var screenshotRectangle = new Rectangle()
            {
                X = rectangle.X + leftPadding,
                Y = rectangle.Y + topPadding,
                Width = rectangle.Width - leftPadding - rightPadding,
                Height = rectangle.Height - topPadding - bottomPadding
            };

            using (Image image = new Bitmap(screenshotRectangle.Width, screenshotRectangle.Height))
            {
                using (Graphics graphics = Graphics.FromImage(image))
                {
                    graphics.CopyFromScreen(new Point
                    (screenshotRectangle.Left, screenshotRectangle.Top), Point.Empty, screenshotRectangle.Size);
                }
                return new Bitmap(image);
            }
        }

        private List<string> ReadDictionary(string dictionaryName)
        {
            var dictionary = new List<string>();

            foreach (var word in File.ReadAllLines(dictionaryName))
            {
                if (word.Length != 5) continue;

                var key = word.ToUpper().Replace("Á", "A").Replace("É", "E").Replace("Í", "I")
                    .Replace("Ó", "O").Replace("Ú", "U").Replace("È", "E").Replace("Ê", "E")
                    .Replace("À", "A").Replace("Ï", "I");

                if (!dictionary.Contains(key))
                    dictionary.Add(key);
            }

            return dictionary;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            input = new WindowsInput.InputSimulator();
        }
    }
}