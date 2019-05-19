using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommonLanguageInterface;
using System.Reflection;

namespace WonbeLib
{
    public abstract class WonbeInterToken
    {
        public readonly int LineNumber;
        public bool IsKeyword(string name)
        {
            var kw = this as KeywordWonbeInterToken;
            return kw != null && kw.Assoc.TargetString == name;
        }
        public bool IsEndOfStatement()
        {
            if (this is EOLWonbeInterToken) return true;
            return this is StringWonbeInterToken && GetChar() == ':';
        }

        public bool IsChar(char name)
        {
            var ch = this as StringWonbeInterToken;
            return ch != null && ch.TargetString == name;
        }
        public bool IsCharInRange(char fromIncluded, char toIncluded)
        {
            var ch = this as StringWonbeInterToken;
            return ch != null && ch.TargetString >= fromIncluded && ch.TargetString <= toIncluded;
        }
        public char GetChar()
        {
            var ch = this as StringWonbeInterToken;
            return ch != null ? ch.TargetString : '\0';
        }
        public WonbeInterToken(int lineNumber)
        {
            this.LineNumber = lineNumber;
        }
    }

    public class EOLWonbeInterToken : WonbeInterToken
    {
        public EOLWonbeInterToken(int lineNumber) : base(lineNumber) { }
    }

    public class NumericalWonbeInterToken : WonbeInterToken
    {
        public readonly int TargetNumber;
        public NumericalWonbeInterToken(int lineNumber, int targetNumber) : base(lineNumber)
        {
            this.TargetNumber = targetNumber;
        }
    }

    public class StringWonbeInterToken : WonbeInterToken
    {
        public readonly char TargetString;
        public StringWonbeInterToken(int lineNumber, char targetString)
            : base(lineNumber)
        {
            this.TargetString = targetString;
        }
    }

    public class LiteralWonbeInterToken : WonbeInterToken
    {
        public readonly string TargetString;
        public LiteralWonbeInterToken(int lineNumber, string targetString)
            : base(lineNumber)
        {
            this.TargetString = targetString;
        }
    }

    public class WhitespaceWonbeInterToken : WonbeInterToken
    {
        public readonly string TargetString;
        public WhitespaceWonbeInterToken(int lineNumber, string targetString)
            : base(lineNumber)
        {
            this.TargetString = targetString;
        }
    }

    public class KeywordWonbeInterToken : WonbeInterToken
    {
        public readonly KeywordAssociation Assoc;
        public KeywordWonbeInterToken(int lineNumber, KeywordAssociation assoc)
            : base(lineNumber)
        {
            this.Assoc = assoc;
        }
    }

    public class StoredSourcecodeLine
    {
        public int LineNumber;
        public WonbeInterToken[] InterimTokens;
        public StoredSourcecodeLine NextLine;
    }

    public class KeywordAssociation
    {
        public readonly string TargetString;
        public readonly Func<Task> TargetAction;
        public KeywordAssociation(string targetString, Func<Task> targetAction = null)
        {
            this.TargetString = targetString;
            this.TargetAction = targetAction;
        }
    }

    public class Wonbe
    {
        private const string myVersion = "0.10";

        /* グローバル変数領域 */
        private const int NUMBER_OF_SIMPLE_VARIABLES = 'Z' - 'A' + 1;

        private short[] globalVariables = new short[NUMBER_OF_SIMPLE_VARIABLES];
        /* gosubする前に有効なローカル変数 */
        private short[] topLevelVariables = new short[NUMBER_OF_SIMPLE_VARIABLES];
        /* あるスコープで有効なローカル変数領域を持つ */
        private short[] localVariables = new short[NUMBER_OF_SIMPLE_VARIABLES];

        /* 現在実行中の位置に関する情報 */
        /* 配列intermeditateExecitionLineのインデクス */
        private StoredSourcecodeLine currentExecutionLineImpl = null;
        private StoredSourcecodeLine CurrentExecutionLine => currentExecutionLineImpl;
        private StoredSourcecodeLine ImmediateExcecutionModeLine { get; set; }
        private int intermeditateExecutionPointer = 0;
        private WonbeInterToken[] intermeditateExecitionLine;
        /* cont用情報 */
        private StoredSourcecodeLine resumeLine { get; set; }
        private int resumeExecutionPointer = 0;

        // for interpreter mode
        private void updateCurrentExecutionLine(StoredSourcecodeLine newExecutionLine, int pointer = 0)
        {
            if (newExecutionLine == null)
            {
                // no line to execute
                currentExecutionLineImpl = null;
                intermeditateExecitionLine = null;
                intermeditateExecutionPointer = 0;
            }
            else
            {
                // set line to execute 
                currentExecutionLineImpl = newExecutionLine;
                intermeditateExecutionPointer = pointer;
                intermeditateExecitionLine = currentExecutionLineImpl.InterimTokens;
            }
        }

        // for immediate mode
        private void updateCurrentExecutionLine(WonbeInterToken[] tokens)
        {
            currentExecutionLineImpl = new StoredSourcecodeLine() { InterimTokens = tokens, LineNumber = 0 };
            intermeditateExecutionPointer = 0;
            intermeditateExecitionLine = tokens;
        }

        /* 保存されているソースコードの全ての行 */
        private List<StoredSourcecodeLine> StoredSource = new List<StoredSourcecodeLine>();

        /* 現在処理中の行番号 (0ならダイレクトモード) */
        private ushort currentLineNumber
        {
            get
            {
                if (CurrentExecutionLine == null) return 0;
                return (ushort)CurrentExecutionLine.LineNumber;
            }
        }

        private Random random = new Random();

        internal enum StackType { Gosub, For };
        internal struct STACK
        {
            internal StackType type;	/* 1==GOSUB, 2==FOR */
            /* for GOSUB and FOR */
            internal int returnPointer;
            /* for GOSUB */
            internal short[] lastLocalVariables;
            //internal short[] simpleVariables = new short[NUMBER_OF_SIMPLE_VARIABLES];
            /* for FOR */
            internal Action<short> setvar;	/* counter variable */
            internal Func<short> getvar;	/* counter variable */
            internal short limit;	/* limit value */
            internal short step;        /* step value */
            internal StoredSourcecodeLine returnExecutionLine;
        };
        private const int STACK_MAX = 8;
        private STACK[] stacks = new STACK[STACK_MAX];
        private int stackPointer;

        // トレースモード(tron/troff)
        bool traceFlag = false;

        private bool bForceToReturnSuperImpl = false;
        private bool bForceToExitImpl = false;
        private bool bInteractiveImpl = true;
        private bool bForceToReturnSuper => bForceToReturnSuperImpl;
        private bool bForceToExit => bForceToExitImpl;
        private bool bInteractive => bInteractiveImpl;

        // モード切替
        private void gotoInteractiveMode()
        {
            bInteractiveImpl = true;
            bForceToReturnSuperImpl = true;
            bForceToExitImpl = false;
        }
        private void gotoInterpreterMode()
        {
            bInteractiveImpl = false;
            bForceToReturnSuperImpl = true;
            bForceToExitImpl = false;
        }
        private void exitProcess()
        {
            bInteractiveImpl = true;
            bForceToReturnSuperImpl = true;
            bForceToExitImpl = true;
        }
        private void clearModes()
        {
            bForceToReturnSuperImpl = false;
            bForceToExitImpl = false;
        }

        /* エラー発生 */
        private async Task<bool> reportError(string errorType)
        {
            if (bInteractive || CurrentExecutionLine == null)
            {
                await Environment.WriteLineAsync("{0}", errorType);
            }
            else
            {
                var sourceCode = decompile(CurrentExecutionLine);
                await Environment.WriteLineAsync("{0} in {1}\r\n{2}", errorType, CurrentExecutionLine.LineNumber, sourceCode);
            }
            gotoInteractiveMode();
            return false;
        }

        private async Task<bool> syntaxError() { return await reportError("Syntax Error"); }
        private async Task divideByZero() { await reportError("Divide by 0"); }
        private async Task outOfArraySubscription() { await reportError("Out of Array Subscription"); }
        private async Task stackOverflow() { await reportError("Stack Overflow"); }
        private async Task stackUnderflow() { await reportError("Stack Underflow"); }
        private async Task nextWithoutFor() { await reportError("Next without For"); }
        private async Task outOfMemory() { await reportError("Out of memory"); }
        private async Task paramError() { await reportError("Parameter Error"); }
        private async Task lineNumberNotFound(ushort lineNumber)
        {
            await reportError(string.Format("Line Number {0} not Found", lineNumber));
        }
        private async Task fileNotFound() { await reportError("File Not Found"); }
        private async Task canNotSave() { await reportError("Can Not Save"); }
        private async Task cantContinue() { await reportError("Can't Continue"); }
        private async Task breakMessage(ushort lineNumber)
        {
            if (lineNumber == 0)
                await Environment.OutputStringAsync($"Break\r\n");
            else
                await Environment.OutputStringAsync($"Break in {lineNumber}\r\n");
        }

        int skipToEOL(int p)
        {
            for (; ; )
            {
                if (p >= intermeditateExecitionLine.Length) return p;
                if (intermeditateExecitionLine[p] is EOLWonbeInterToken) return p;
                p++;
            }
        }

        WonbeInterToken skipEPToNonWhiteSpace()
        {
            for (; ; )
            {
                if (intermeditateExecutionPointer >= intermeditateExecitionLine.Length) return new EOLWonbeInterToken(0);
                var token = intermeditateExecitionLine[intermeditateExecutionPointer++];
                if (token is WhitespaceWonbeInterToken) continue;
                var strToken = token as StringWonbeInterToken;
                if (strToken == null) return token;
                if (strToken.TargetString != ' ' && strToken.TargetString != '\t') return token;
            }
        }

        int mytolower(byte ch)
        {
            if (ch >= 'A' && ch <= 'Z') return ch - 'A' + 'a';
            return ch;
        }

        /* 実行時情報のクリア (contできなくなる) */
        void clearRuntimeInfo(StoredSourcecodeLine line = null)
        {
            for (int i = 0; i < globalVariables.Length; i++) globalVariables[i] = 0;
            for (int i = 0; i < topLevelVariables.Length; i++) topLevelVariables[i] = 0;
            //for (int i = 0; i < topLevelVariables.Length; i++) topLevelVariables[i] = 0;
            localVariables = topLevelVariables;
            stackPointer = 0;
            random = new Random((int)DateTime.Now.Ticks);
            /* Create Line Links */
            StoredSourcecodeLine last = null;
            foreach (var item in StoredSource)
            {
                if (last != null) last.NextLine = item;
                last = item;
                last.NextLine = null;
            }
            /* Init Execution pointer */
            if (line == null) line = StoredSource.Count == 0 ? null : StoredSource[0];
            updateCurrentExecutionLine(line);
            // clear cont info
            resumeLine = null;
            resumeExecutionPointer = 0;
        }

        /* 行頭の行番号の、実行時の定型処理 */
        async Task processLineHeader()
        {
            // tron support
            if (traceFlag && intermeditateExecitionLine.Length > intermeditateExecutionPointer)
            {
                await Environment.OutputStringAsync($"[{currentLineNumber}]");
            }
        }

        /* 行番号処理 */
        StoredSourcecodeLine getLineReferenceFromLineNumber(ushort lineNumber)
        {
            return StoredSource.FirstOrDefault(c => c.LineNumber == lineNumber);
        }

        /* 配列管理 */
        private const int availableArrayItems = 1024;
        private short[] array = new short[availableArrayItems];

        public LanguageBaseEnvironmentInfo Environment { get; private set; }

        /// <summary>
        /// 配列のインデックスを解析する
        /// </summary>
        /// <returns>配列の添え字。無効の場合はnull</returns>
        async Task<int?> getArrayReference()
        {
            int index;
            var token = skipEPToNonWhiteSpace() as StringWonbeInterToken;
            if (token != null && token.TargetString != '(' && token.TargetString != '[')
            {
                await syntaxError();
                return null;
            }
            index = await expr();
            if (bForceToReturnSuper) return null;

            if (index < 0 || index >= array.Length)
            {
                await outOfArraySubscription();
                return null;	/* そのインデックスは使えません */
            }

            var token2 = skipEPToNonWhiteSpace() as StringWonbeInterToken;
            if (token2 != null && token2.TargetString != ')' && token2.TargetString != ']')
            {
                await syntaxError();
                return null;
            }
            return index;
        }

        /* 式計算機能 */
        async Task<short> calcValue()
        {
            var token = skipEPToNonWhiteSpace();
            if (token.IsCharInRange('A', 'Z'))
            {
                return globalVariables[token.GetChar() - 'A'];
            }
            if (token.IsCharInRange('a', 'z'))
            {
                return localVariables[token.GetChar() - 'a'];
            }
            if (token.GetChar() == '@')
            {
                int index = await getArrayReference() ?? -1;
                if (bForceToReturnSuper) return -1;
                return array[index];
            }
            if (token.IsKeyword("not")) return (short)~await calcValue();
            if (token.IsKeyword("rnd")) return (short)random.Next(await calcValue());
            if (token.IsKeyword("abs"))
            {
                short t = await calcValue();
                if (t < 0) return (short)-t;
                return t;
            }
            if (token.IsKeyword("tick")) return (short)(DateTime.Now.Ticks / 10);
            if (token.IsKeyword("textwidth")) return (short)await Environment.GetTextWidthAsync();
            if (token.IsKeyword("textheight")) return (short)await Environment.GetTextHeightAsync();
            if (token is NumericalWonbeInterToken)
            {
                return (short)(token as NumericalWonbeInterToken).TargetNumber;
            }
            switch ((int)token.GetChar())
            {
                case '(':
                    {
                        short t;
                        t = await expr();
                        var token2 = skipEPToNonWhiteSpace();
                        if (token2.GetChar() != ')') break;
                        return t;
                    }
                case '-':
                    return (short)(-await calcValue());
            }
            await syntaxError();
            return -1;
        }

        private async Task<short> expr4th()
        {
            short acc;
            acc = await calcValue();
            if (bForceToReturnSuper) return -1;

            for (; ; )
            {
                var token = skipEPToNonWhiteSpace();
                switch ((int)token.GetChar())
                {
                    case '*':
                        acc = (short)(acc * await calcValue());
                        break;
                    case '/':
                        {
                            short t;
                            t = await calcValue();
                            if (t == 0)
                            {
                                await divideByZero();
                            }
                            else
                            {
                                acc = (short)(acc / t);
                            }
                        }
                        break;
                    default:
                        intermeditateExecutionPointer--;		/* unget it */
                        return acc;
                }
                if (bForceToReturnSuper) return -1;
            }
        }

        async Task<short> expr3rd()
        {
            short acc;
            acc = await expr4th();
            if (bForceToReturnSuper) return -1;

            for (; ; )
            {
                var token = skipEPToNonWhiteSpace();
                switch ((int)token.GetChar())
                {
                    case '+':
                        acc = (short)(acc + await expr4th());
                        break;
                    case '-':
                        acc = (short)(acc - await expr4th());
                        break;
                    default:
                        intermeditateExecutionPointer--;		/* unget it */
                        return acc;
                }
                if (bForceToReturnSuper) return -1;
            }
        }

        async Task<short> expr2nd()
        {
            short acc;
            acc = await expr3rd();
            if (bForceToReturnSuper) return -1;

            for (; ; )
            {
                var token = skipEPToNonWhiteSpace();
                switch ((int)token.GetChar())
                {
                    case '>':
                        {
                            var token2 = skipEPToNonWhiteSpace();
                            if (token2.GetChar() == '=')
                            {
                                acc = (short)(acc >= await expr3rd() ? 1 : 0);
                                if (acc != 0) acc = -1;
                            }
                            else
                            {
                                intermeditateExecutionPointer--;
                                acc = (short)(acc > await expr3rd() ? 1 : 0);
                                if (acc != 0) acc = -1;
                            }
                        }
                        break;
                    case '<':
                        {
                            var token2 = skipEPToNonWhiteSpace();
                            if (token2.GetChar() == '=')
                            {
                                acc = (short)(acc <= await expr3rd() ? 1 : 0);
                                if (acc != 0) acc = -1;
                            }
                            else if (token2.GetChar() == '>')
                            {
                                acc = (short)(acc != await expr3rd() ? 1 : 0);
                                if (acc != 0) acc = -1;
                            }
                            else
                            {
                                intermeditateExecutionPointer--;
                                acc = (short)(acc < await expr3rd() ? 1 : 0);
                                if (acc != 0) acc = -1;
                            }
                        }
                        break;
                    case '=':
                        acc = (short)(acc == await expr3rd() ? 1 : 0);
                        if (acc != 0) acc = -1;
                        break;
                    default:
                        intermeditateExecutionPointer--;		/* unget it */
                        return acc;
                }
                if (bForceToReturnSuper) return -1;
            }
        }

        async Task<short> expr()
        {
            short acc;
            acc = await expr2nd();
            if (bForceToReturnSuper) return -1;

            for (; ; )
            {
                var token = skipEPToNonWhiteSpace();
                if (token.IsKeyword("and"))
                    acc = (short)(acc & await expr2nd());
                else if (token.IsKeyword("or"))
                    acc = (short)(acc | await expr2nd());
                else if (token.IsKeyword("xor"))
                    acc = (short)(acc ^ await expr2nd());
                else
                {
                    intermeditateExecutionPointer--;     /* unget it */
                    return acc;
                }
                if (bForceToReturnSuper) return -1;
            }
        }

        /* 各ステートメント実行処理メイン */

        async Task st_assignment(Action<short> setter)	/* 代入ステートメントだけ例外的に処理する */
        {
            var token = skipEPToNonWhiteSpace();
            if (token.GetChar() != '=')
            {
                await syntaxError();
                return;
            }
            short val = await expr();
            if (bForceToReturnSuper) return;
            setter(val);
        }

        async Task printOrDebug(Func<char,Task> outputChar, Func<string, Task> outputString, Func<Task> outputNewLine)
        {
            char lastChar = '\0';
            for (; ; )
            {
                var token = skipEPToNonWhiteSpace();
                var strToken = token as StringWonbeInterToken;
                if (strToken != null && (strToken.TargetString == ':' || strToken.TargetString == '\''))
                {
                    intermeditateExecutionPointer--;	/* unget it */
                    break;
                }
                if (token is EOLWonbeInterToken)
                {
                    intermeditateExecutionPointer--;	/* unget it */
                    break;
                }

                lastChar = '\0';
                if (strToken != null) lastChar = strToken.TargetString;

                if (token is LiteralWonbeInterToken)
                {
                    await outputString((token as LiteralWonbeInterToken).TargetString);
                }
                else if (token.IsKeyword("chr"))
                {
                    ushort val = (ushort)await expr();
                    if (bForceToReturnSuper) return;
                    await outputChar((char)val);
                }
                else
                {
                    switch (token.GetChar())
                    {
                        case ';':
                            break;
                        case ',':
                            await outputChar('\t');
                            break;
                        default:
                            {
                                short val;
                                intermeditateExecutionPointer--;	/* unget it */
                                val = await expr();
                                if (bForceToReturnSuper) return;
                                await outputString($"{val}");
                            }
                            break;
                    }
                }
            }
            if (lastChar != ';' && lastChar != ',')
            {
                await outputNewLine();
            }
        }

        private async Task st_debug() => await printOrDebug((ch) => Environment.DebugOutputStringAsync(ch.ToString()), Environment.DebugOutputStringAsync, () => Environment.DebugOutputStringAsync("\r\n"));
        private async Task st_print() => await printOrDebug(Environment.OutputCharAsync, Environment.OutputStringAsync, () => Environment.WriteLineAsync());

        async Task st_goto()
        {
            short val;
            val = await expr();
            if (bForceToReturnSuper) return;
            var t = getLineReferenceFromLineNumber((ushort)val);
            if (t == null)
            {
                await lineNumberNotFound((ushort)val);
                return;
            }
            updateCurrentExecutionLine(t);
            if (bInteractive) gotoInterpreterMode();
            await processLineHeader();
        }

        async Task st_gosub()
        {
            short val;
            val = await expr();
            if (bForceToReturnSuper) return;
            var t = getLineReferenceFromLineNumber((ushort)val);
            if (t == null)
            {
                await lineNumberNotFound((ushort)val);
                return;
            }
            if (stackPointer + 1 >= STACK_MAX)
            {
                await stackOverflow();
                return;
            }
            stacks[stackPointer].type = StackType.Gosub;
            stacks[stackPointer].returnPointer = intermeditateExecutionPointer;
            stacks[stackPointer].returnExecutionLine = CurrentExecutionLine;
            stacks[stackPointer].lastLocalVariables = localVariables;
            localVariables = new short[NUMBER_OF_SIMPLE_VARIABLES];
            stackPointer++;
            updateCurrentExecutionLine(t);
            if (bInteractive) gotoInterpreterMode();
            await processLineHeader();
        }

        async Task st_return()
        {
            for (; ; )
            {
                if (stackPointer == 0)
                {
                    await stackUnderflow();
                    return;
                }
                stackPointer--;
                if (stacks[stackPointer].type == StackType.Gosub) break;
            }
            updateCurrentExecutionLine(stacks[stackPointer].returnExecutionLine, stacks[stackPointer].returnPointer);
            localVariables = stacks[stackPointer].lastLocalVariables;
        }

        async Task st_if()
        {
            short val = await expr();
            if (bForceToReturnSuper) return;
            var token = skipEPToNonWhiteSpace();
            if (!token.IsKeyword("then"))
            {
                await syntaxError();
                return;
            }
            if (val != 0)
            {
                var token2 = skipEPToNonWhiteSpace();
                if (token2 is NumericalWonbeInterToken)
                {
                    // thenのあとに整数が直接書かれた場合は、それにgotoする。
                    intermeditateExecutionPointer--;
                    await st_goto();
                    return;
                }
                intermeditateExecutionPointer--;
                return;	/* thenの次から継続実行する */
            }
            /* 条件不成立につき、行末まで読み飛ばす */
            intermeditateExecutionPointer = skipToEOL(intermeditateExecutionPointer);
        }

        async Task st_for()
        {
            short from, to, step;
            Action<short> setvar;
            Func<short> getvar;
            var token = skipEPToNonWhiteSpace();
            if (token.GetChar() == '@')
            {	/* is it l-value? */
                int? pvar = await getArrayReference();
                if (pvar == null) return;
                setvar = c => { array[(int)pvar] = c; };
                getvar = () => array[(int)pvar];
            }
            else if (token.IsCharInRange('A', 'Z'))
            {
                var ch0 = token.GetChar();
                setvar = c =>
                {
                    globalVariables[ch0 - 'A'] = c;
                };
                getvar = () => globalVariables[ch0 - 'A'];
            }
            else if (token.IsCharInRange('a', 'z'))
            {
                var ch0 = token.GetChar();
                setvar = c =>
                {
                    localVariables[ch0 - 'a'] = c;
                };
                getvar = () => localVariables[ch0 - 'a'];
            }
            else
            {
                await syntaxError();
                return;
            }
            var token2 = skipEPToNonWhiteSpace();
            if (token2.GetChar() != '=')
            {
                await syntaxError();
                return;
            }
            from = await expr();
            if (bForceToReturnSuper) return;
            var token3 = skipEPToNonWhiteSpace();
            if (!token3.IsKeyword("to"))
            {
                await syntaxError();
                return;
            }
            to = await expr();
            if (bForceToReturnSuper) return;
            var token4 = skipEPToNonWhiteSpace();
            if (token4.IsKeyword("step"))
            {
                step = await expr();
                if (bForceToReturnSuper) return;
            }
            else
            {
                step = 1;
                intermeditateExecutionPointer--;	/* unget it */
            }

            if (stackPointer + 1 >= STACK_MAX)
            {
                await stackOverflow();
                return;
            }
            stacks[stackPointer].type = StackType.For;
            stacks[stackPointer].returnPointer = intermeditateExecutionPointer;
            stacks[stackPointer].returnExecutionLine = CurrentExecutionLine;
            setvar(from);
            stacks[stackPointer].setvar = setvar;
            stacks[stackPointer].getvar = getvar;
            stacks[stackPointer].limit = to;
            stacks[stackPointer].step = step;
            stackPointer++;
        }

        async Task st_next()
        {
            if (stackPointer == 0)
            {
                await nextWithoutFor();
                return;
            }
            if (stacks[stackPointer - 1].type != StackType.For)
            {
                await nextWithoutFor();
                return;
            }
            if (stacks[stackPointer - 1].limit == stacks[stackPointer - 1].getvar())
            {
                /* loop done */
                stackPointer--;
                return;
            }
            /* count step and loop again */
            stacks[stackPointer - 1].setvar((short)(stacks[stackPointer - 1].getvar() + stacks[stackPointer - 1].step));
            /* counter overflow? */
            if (stacks[stackPointer - 1].step > 0)
            {
                if (stacks[stackPointer - 1].limit < stacks[stackPointer - 1].getvar())
                {
                    /* loop done */
                    stackPointer--;
                    return;
                }
            }
            else
            {
                if (stacks[stackPointer - 1].limit > stacks[stackPointer - 1].getvar())
                {
                    /* loop done */
                    stackPointer--;
                    return;
                }
            }
            updateCurrentExecutionLine(stacks[stackPointer - 1].returnExecutionLine, stacks[stackPointer - 1].returnPointer);
        }

        /* endステートメント:　正常な終了 */
        async Task st_end()
        {
            gotoInteractiveMode();
            await Task.Delay(0);
        }

        /* breakステートメント:　デバッグ用の中断 */
        async Task st_break()
        {
            if (currentLineNumber > 0)
            {
                resumeLine = currentExecutionLineImpl;
                resumeExecutionPointer = intermeditateExecutionPointer;
            }
            await breakMessage(currentLineNumber);
            gotoInteractiveMode();
            await Task.Delay(0);
        }

        async Task st_rem()
        {
            intermeditateExecutionPointer = skipToEOL(intermeditateExecutionPointer);
            await Task.Delay(0);
        }

        async Task st_randomize()
        {
            short val;
            val = await expr();
            this.random = new Random(val);
        }

        async Task st_exit()
        {
            exitProcess();
            await Task.Delay(0);
        }

        async Task st_waitms()
        {
            short val;
            val = await expr();
            if (bForceToReturnSuper) return;
            if (val < 0 || val > 3000)
            {
                await paramError();
                return;
            }
            await System.Threading.Tasks.Task.Delay(val);
        }

        async Task st_tron()
        {
            traceFlag = true;
            await Task.Delay(0);
        }

        async Task st_troff()
        {
            traceFlag = false;
            await Task.Delay(0);
        }

        private string decompile(StoredSourcecodeLine line)
        {
            var sb = new StringBuilder();
            sb.Append($"{line.LineNumber} ");
            foreach (var item in line.InterimTokens)
            {
                if (item is NumericalWonbeInterToken)
                {
                    sb.Append($"{(item as NumericalWonbeInterToken).TargetNumber}");
                }
                else if (item is StringWonbeInterToken)
                {
                    sb.Append($"{(item as StringWonbeInterToken).TargetString}");
                }
                else if (item is WhitespaceWonbeInterToken)
                {
                    sb.Append($"{(item as WhitespaceWonbeInterToken).TargetString}");
                }
                else if (item is LiteralWonbeInterToken)
                {
                    sb.Append($"\"{(item as LiteralWonbeInterToken).TargetString}\"");
                }
                else if (item is KeywordWonbeInterToken)
                {
                    sb.Append($"{(item as KeywordWonbeInterToken).Assoc.TargetString}");
                }
            }
            return sb.ToString();
        }

        private async Task sourceDump(Func<string, Task> writeAsync, int startLineNumber, int endLineNumber)
        {
            foreach (var lineInfo in StoredSource.Where(c => c.LineNumber >= startLineNumber && c.LineNumber <= endLineNumber))
            {
                await writeAsync(decompile(lineInfo));
            }
        }

        private async Task st_list()
        {
            int from = 0, to = int.MaxValue;
            var token = skipEPToNonWhiteSpace();
            if (!token.IsEndOfStatement())
            {
                WonbeInterToken token2;
                if (token is NumericalWonbeInterToken)
                {
                    from = (token as NumericalWonbeInterToken).TargetNumber;
                    token2 = skipEPToNonWhiteSpace();
                }
                else if (token.GetChar() == '-')
                {
                    token2 = token;
                }
                else
                {
                    await syntaxError();
                    return;
                }
                if (!token2.IsEndOfStatement())
                {
                    var sepToken = token2 as StringWonbeInterToken;
                    if (sepToken == null)
                    {
                        await syntaxError();
                        return;
                    }
                    if (sepToken.TargetString != '-')
                    {
                        await syntaxError();
                        return;
                    }
                    else
                    {
                        var token3 = skipEPToNonWhiteSpace();
                        if (!token3.IsEndOfStatement())
                        {
                            var toToken = token3 as NumericalWonbeInterToken;
                            if (toToken == null)
                            {
                                await syntaxError();
                                return;
                            }
                            else
                            {
                                to = toToken.TargetNumber;  // case of "LIST [LINE_NUMBER]-[LINE_NUMBER]"
                            }
                        }
                    }
                }
                else
                {
                    to = from;  // case of "LIST [LINE_NUMBER]"
                }
            }
            await sourceDump(async (line) =>
            {
                await Environment.WriteLineAsync(line);
            }, from, to);	/* リスト出力の本体を呼ぶ */
        }

        private async Task st_new()
        {
            clearRuntimeInfo();
            StoredSource.Clear();
            gotoInteractiveMode();
            await Task.Delay(0);
        }

        private async Task<string> getNextFileName()
        {
            var token = skipEPToNonWhiteSpace() as LiteralWonbeInterToken;
            if (token == null)
            {
                await syntaxError();
                return null;
            }
            return token.TargetString;
        }

        private bool isResourcePath(string s) => s.ToLower().StartsWith("res:");

        private async Task loadAndRunCommon(string filename, bool requireNew = true)
        {
            Stream stream = null;
            if (isResourcePath(filename))
            {
                filename = filename.Substring(4);
                if (!Path.HasExtension(filename)) filename = filename + ".wb";
                stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("WonbeLib." + filename);
            }
            else
            {
                if (!Path.HasExtension(filename)) filename = filename + ".wb";
                stream = await Environment.LoadAsync(filename);
            }
            if (stream == null)
            {
                await fileNotFound();
                return;
            }
            clearRuntimeInfo();
            if (requireNew) StoredSource.Clear();
            using (stream)
            {
                using (var reader = new StreamReader(stream))
                {
                    for (; ; )
                    {
                        if (bForceToExit) return;
                        if (bForceToReturnSuper) return;
                        var s = await reader.ReadLineAsync();
                        if (s == null) break;
                        if (string.IsNullOrWhiteSpace(s)) continue;
                        var dstList = new List<WonbeInterToken>();

                        var r = await parseLine(s, dstList);
                        if (r.requestToContinue) continue;

                        /* 数値で開始されているか? */
                        if (r.hasLineNumber)
                        {
                            /* 行エディタを呼び出す */
                            await editLineWithoutSort(r.hasLineNumber, r.lineNumber, dstList);
                            clearRuntimeInfo();
                        }
                        else
                        {
                            await syntaxError();
                            return;
                        }
                    }
                }
            }
            sortSourceLines();
            gotoInteractiveMode();
        }
        private async Task loadAndMergeCommon(bool requireNew)
        {
            string filename = await getNextFileName();
            if (bForceToReturnSuper || filename == null) return;
            await loadAndRunCommon(filename, requireNew);
        }

        private async Task st_load() => await loadAndMergeCommon(true);

        private async Task st_merge() => await loadAndMergeCommon(false);

        private async Task st_run()
        {
            StoredSourcecodeLine startLine = null;
            var token = skipEPToNonWhiteSpace();
            if (token != null)
            {
                if (token is LiteralWonbeInterToken)
                {
                    await loadAndRunCommon((token as LiteralWonbeInterToken).TargetString);
                    clearRuntimeInfo(startLine);
                    gotoInterpreterMode();
                    return;
                }
                else if (token is NumericalWonbeInterToken)
                {
                    int n = (token as NumericalWonbeInterToken).TargetNumber;
                    foreach (var item in StoredSource)
                    {
                        if (item.LineNumber == n)
                        {
                            startLine = item;
                            break;
                        }
                    }
                }
                else if (!(token is EOLWonbeInterToken))
                {
                    await syntaxError();
                    return;
                }
            }
            if (StoredSource.Count == 0) return;    // if no lines in source, do nothing
            clearRuntimeInfo(startLine);
            gotoInterpreterMode();
        }

        private async Task st_save()
        {
            string filename = await getNextFileName();
            if (bForceToReturnSuper || filename == null) return;
            if (isResourcePath(filename))
            {
                await paramError();
                return;
            }
            if (!Path.HasExtension(filename)) filename = filename + ".wb";
            Stream stream = await Environment.SaveAsync(filename);
            if (stream == null)
            {
                await canNotSave();
                return;
            }
            using (stream)
            {
                using (var writer = new StreamWriter(stream))
                {
                    await sourceDump(async (line) =>
                    {
                        await writer.WriteLineAsync(line);
                    }, int.MinValue, int.MaxValue);	/* リスト出力の本体を呼ぶ */
                }
            }
        }

        private async Task st_files()
        {
            string path = null;
            var token = skipEPToNonWhiteSpace();
            if (token != null)
            {
                if (token is LiteralWonbeInterToken)
                {
                    path = (token as LiteralWonbeInterToken).TargetString;
                }
                else if (!(token is EOLWonbeInterToken))
                {
                    await syntaxError();
                    return;
                }
            }
            IEnumerable<string> e = null;
            if (path != null && isResourcePath(path))
            {
                if (path.Length != 4)    // "res:" only
                {
                    await paramError();
                    return;
                }
                e = Assembly.GetExecutingAssembly().GetManifestResourceNames().Select(c => c.Substring("WonbeLib.".Length));
            }
            else
            {
                e = await Environment.FilesAsync(path);
                if (e == null)
                {
                    await paramError();
                    return;
                }
            }
            int pos = 0;
            foreach (var item in e)
            {
                var name = Path.GetFileName(item);
                var spaces = 9 - name.Length % 8;
                var s = name + new string(' ', spaces);
                if (pos + s.Length > 80)
                {
                    await Environment.OutputStringAsync("\r\n");
                    pos = 0;
                }
                await Environment.OutputStringAsync(s);
                pos += s.Length;
            }
            if (pos != 0) await Environment.OutputStringAsync("\r\n");
        }

        private async Task st_cont()
        {
            if (resumeLine == null || !bInteractive)
            {
                await cantContinue();
                return;
            }
            updateCurrentExecutionLine(resumeLine, resumeExecutionPointer);
            gotoInterpreterMode();
        }

        private async Task skipComma()
        {
            var t = skipEPToNonWhiteSpace() as StringWonbeInterToken;
            if (t == null || t.TargetString != ',') await syntaxError();
        }

        private async Task colorCommon(Func<LanguageBaseColor, Task> setColor)
        {
            int r, g, b, a = 255;
            r = await expr();
            if (bForceToReturnSuper) return;
            await skipComma();
            if (bForceToReturnSuper) return;
            g = await expr();
            if (bForceToReturnSuper) return;
            await skipComma();
            if (bForceToReturnSuper) return;
            b = await expr();
            if (bForceToReturnSuper) return;
            var t = skipEPToNonWhiteSpace() as StringWonbeInterToken;
            if (t != null && t.TargetString == ',')
            {
                a = await expr();
                if (bForceToReturnSuper) return;
            }
            else
            {
                intermeditateExecutionPointer--;    // unget token
            }
            await setColor(new LanguageBaseColor((byte)r, (byte)g, (byte)b, (byte)a));
        }

        private async Task st_color() => await colorCommon(async (c) => await Environment.SetForeColorAsync(c));

        private async Task st_backcolor() => await colorCommon(async (c) => await Environment.SetBackColorAsync(c));

        private async Task st_locate()
        {
            int x, y;
            x = await expr();
            if (bForceToReturnSuper) return;
            await skipComma();
            if (bForceToReturnSuper) return;
            y = await expr();
            if (bForceToReturnSuper) return;
            bool r = await Environment.LocateAsync(x, y);
            if (r == false) await paramError();
        }
        private async Task st_cls() {
            await Environment.ClearScreen();
        }


        private async Task st_waitvb() { throw new NotImplementedException(); }
        private async Task st_play() { throw new NotImplementedException(); }

        public KeywordAssociation searchToken(string srcLine, int from, KeywordAssociation[] assocTable)
        {
            foreach (var n in assocTable)
            {
                if (srcLine.Length < from + n.TargetString.Length) continue;
                if (srcLine.Substring(from, n.TargetString.Length).ToLower() == n.TargetString) return n;
            }
            return null;
        }

        /* 中間言語に翻訳する */
        async Task<bool> convertInternalCode(string srcLine, List<WonbeInterToken> dst, int lineNumber)
        {
            KeywordAssociation[] AssocTable =
                {
                    new KeywordAssociation("if",st_if),
                    new KeywordAssociation("print",st_print),
                    new KeywordAssociation("locate",st_locate),
                    new KeywordAssociation("cls",st_cls),
                    new KeywordAssociation("goto",st_goto),
                    new KeywordAssociation("gosub",st_gosub),
                    new KeywordAssociation("return",st_return),
                    new KeywordAssociation("for",st_for),
                    new KeywordAssociation("next",st_next),
                    new KeywordAssociation("end",st_end),
                    new KeywordAssociation("break",st_break),
                    new KeywordAssociation("rem",st_rem),
                    new KeywordAssociation("randomize",st_randomize),
                    new KeywordAssociation("exit",st_exit),
                    new KeywordAssociation("debug",st_debug),
                    new KeywordAssociation("waitms",st_waitms),
                    new KeywordAssociation("tron",st_tron),
                    new KeywordAssociation("troff",st_troff),

                    new KeywordAssociation("new",st_new),
                    new KeywordAssociation("list",st_list),
                    new KeywordAssociation("run",st_run),
                    new KeywordAssociation("cont",st_cont),
                    new KeywordAssociation("save",st_save),
                    new KeywordAssociation("load",st_load),
                    new KeywordAssociation("merge",st_merge),
                    new KeywordAssociation("waitvb",st_waitvb),
                    new KeywordAssociation("files",st_files),
                    new KeywordAssociation("play",st_play),
                    new KeywordAssociation("color",st_color),
                    new KeywordAssociation("backcolor",st_backcolor),
                    new KeywordAssociation("and"),
                    new KeywordAssociation("or"),
                    new KeywordAssociation("xor"),
                    new KeywordAssociation("not"),
                    new KeywordAssociation("rnd"),
                    new KeywordAssociation("abs"),
                    new KeywordAssociation("tick"),
                    new KeywordAssociation("textwidth"),
                    new KeywordAssociation("textheight"),
                    new KeywordAssociation("then"),
                    new KeywordAssociation("chr"),
                    new KeywordAssociation("to"),
                    new KeywordAssociation("step"),
                };
            int src = 0;
            for (; ; )
            {
                if (src >= srcLine.Length) break;
                int org = src;
                for (; ; )
                {
                    if (srcLine[src] == ' ' || srcLine[src] == '\t')
                    {
                        src++;
                        continue;
                    }
                    break;
                }
                if (org < src) dst.Add(new WhitespaceWonbeInterToken(lineNumber, srcLine.Substring(org, src - org)));
                if (srcLine[src] < 0x20) return await syntaxError();
                char next = (srcLine.Length <= src + 1) ? '\0' : srcLine[src + 1];
                if (srcLine[src] == '0' && next == 'x')
                {
                    int acc = 0, n;
                    src += 2;
                    for (; ; )
                    {
                        if (srcLine[src] >= '0' && srcLine[src] <= '9') n = (ushort)(srcLine[src] - '0');
                        else if (srcLine[src] >= 'a' && srcLine[src] <= 'f') n = (ushort)(srcLine[src] - 'a' + 10);
                        else if (srcLine[src] >= 'A' && srcLine[src] <= 'F') n = (ushort)(srcLine[src] - 'A' + 10);
                        else break;
                        acc *= 16;
                        acc += n;
                        src++;
                    }
                    dst.Add(new NumericalWonbeInterToken(lineNumber, acc));
                }
                else if (srcLine[src] >= '0' && srcLine[src] <= '9')
                {
                    int acc = 0;
                    for (; ; )
                    {
                        if (src >= srcLine.Length) break;
                        if (srcLine[src] < '0' || srcLine[src] > '9') break;
                        acc *= 10;
                        acc += srcLine[src] - '0';
                        src++;
                        // overflow case
                        if (acc < 0) return await syntaxError();
                    }
                    dst.Add(new NumericalWonbeInterToken(lineNumber, acc));
                }
                else if ((srcLine[src] >= 'a' && srcLine[src] <= 'z') || (srcLine[src] >= 'A' && srcLine[src] <= 'Z'))
                {
                    var token = searchToken(srcLine, src, AssocTable);
                    if (token != null)
                    {
                        dst.Add(new KeywordWonbeInterToken(lineNumber, token));
                        src += token.TargetString.Length;
                        // remステートメントなら、そのあとに何が書かれていても無視
                        if (token.TargetString == "rem") return true;
                    }
                    else
                    {
                        dst.Add(new StringWonbeInterToken(lineNumber, srcLine[src++]));
                    }
                }
                // コメントならそのあと何があっても無視
                else if (srcLine[src] == '\'') return true;
                else if (srcLine[src] == '"')
                {
                    src++;
                    var sb = new StringBuilder();
                    for (; ; )
                    {
                        //if( src >= srcLine.Length) return await syntaxError();
                        if (src >= srcLine.Length) break;
                        char v = srcLine[src++];
                        if (v == '"') break;
                        sb.Append(v);
                    }
                    dst.Add(new LiteralWonbeInterToken(lineNumber, sb.ToString()));
                }
                else
                {
                    dst.Add(new StringWonbeInterToken(lineNumber, srcLine[src++]));
                }
            }
            return true;
        }

        async Task interpreterMain()
        {
            for (; ; )
            {
                if (bForceToExit) return;
                /* 行の開始 */
                await processLineHeader();
                /* 最後に達してしまった? */
                if (!bInteractive && currentLineNumber == 0)
                {
                    gotoInteractiveMode();
                    return;
                }

                for (; ; )
                {
                    if (intermeditateExecitionLine == null) break;
                    if (intermeditateExecitionLine.Length <= intermeditateExecutionPointer) break;
                    var token = intermeditateExecitionLine[intermeditateExecutionPointer++];
                    if (token is EOLWonbeInterToken) break;
                    if (token.GetChar() == ' ' || token.GetChar() == '\t' || token.GetChar() == ':')
                    {
                        /* nop */
                    }
                    else if (token.GetChar() == '\'')
                    {	/* comment */
                        await st_rem();
                    }
                    else if (token.GetChar() == '@')
                    {	/* is it l-value? */
                        int? pvar;
                        pvar = await getArrayReference();
                        if (pvar == null) return;
                        await st_assignment(c => { array[(int)pvar] = c; });
                    }
                    else if (token.IsCharInRange('A', 'Z'))
                    {
                        await st_assignment(c =>
                        {
                            globalVariables[token.GetChar() - 'A'] = c;
                        });
                    }
                    else if (token.IsCharInRange('a', 'z'))
                    {
                        await st_assignment(c =>
                        {
                            localVariables[token.GetChar() - 'a'] = c;
                        });
                    }
                    else if (token is KeywordWonbeInterToken)
                    {
                        Func<Task> a = (token as KeywordWonbeInterToken).Assoc.TargetAction;
                        if (a == null)
                        {
                            await syntaxError();
                        }
                        else
                        {
                            await a();
                            if (bForceToReturnSuper) return;
                        }
                    }
                    else if (token.GetChar() == '?')
                    {
                        await st_print();
                    }
                    else
                    {
                        await syntaxError();
                    }
                    if (bForceToReturnSuper) return;
                }
                if (bInteractive) return;
                /* 行が尽きたので実行を終わる */
                if (CurrentExecutionLine == null)
                {
                    gotoInteractiveMode();
                    return;
                }
                /* 行が尽きたので次の行に行く */
                updateCurrentExecutionLine(CurrentExecutionLine.NextLine);
                if (CurrentExecutionLine == null)
                {
                    gotoInteractiveMode();
                    return;
                }
            }
        }

        class parseResult
        {
            internal bool hasLineNumber;
            internal int lineNumber;
            internal bool requestToContinue;
        }

        private async Task<parseResult> parseLine(string s, List<WonbeInterToken> dstList)
        {
            var r = new parseResult();
            // 行番号だけここで解析しないと間に合わない
            r.lineNumber = 0;
            r.hasLineNumber = false;
            int src = 0;
            for (; ; )
            {
                if (s.Length <= src) break;
                if (s[src] < '0' || s[src] > '9') break;
                r.hasLineNumber = true;
                r.lineNumber *= 10;
                r.lineNumber += s[src] - '0';
                src++;
            }
            // skip one whitespace after line number
            if (src < s.Length)
            {
                if (s[src] == ' ' || s[src] == 't') src++;

                /* 中間言語に翻訳する */
                bool b = await convertInternalCode(s.Substring(src), dstList, r.lineNumber);
                if (b == false) { r.requestToContinue = true; return r; }
            }
            if (bForceToReturnSuper) { r.requestToContinue = true; return r; }
            dstList.Add(new EOLWonbeInterToken(r.lineNumber));
            return r;
        }


        private async Task<bool> interactiveMainAsync(List<WonbeInterToken> dstList)
        {
            bool requirePrompt = true;
            for (; ; )
            {
                if (bForceToExit) return true;
                if (!bInteractive) return false;
                if (bForceToReturnSuper) return false;
                dstList.Clear();
                if (requirePrompt) await Environment.WriteLineAsync("OK");
                string s = await Environment.LineInputAsync("");
                if (s == null) return false;
                if (string.IsNullOrWhiteSpace(s)) continue;

                var r = await parseLine(s, dstList);
                if (r.requestToContinue) continue;

                /* 数値で開始されているか? */
                if (r.hasLineNumber)
                {
                    /* 行エディタを呼び出す */
                    await editLine(r.hasLineNumber, r.lineNumber, dstList);
                    clearRuntimeInfo();
                    requirePrompt = false;
                }
                else
                {
                    /* その行を実行する */
                    updateCurrentExecutionLine(dstList.ToArray());
                    await interpreterMain();
                    //bForceToReturnSuper = false;
                    requirePrompt = true;
                }
            }
        }

        private void sortSourceLines()
        {
            // sort all lines by line number
            StoredSource.Sort((x, y) =>
            {
                return x.LineNumber - y.LineNumber;
            });
        }

        private async Task editLineWithoutSort(bool hasLineNumber, int lineNumber, List<WonbeInterToken> dstList)
        {

            var foundLine = StoredSource.FirstOrDefault(c => c.LineNumber == lineNumber);
            if (foundLine == null)
            {
                if (dstList.All(c => c is EOLWonbeInterToken))
                {
                    await lineNumberNotFound((ushort)lineNumber);
                    return;
                }

                // create new line
                StoredSource.Add(new StoredSourcecodeLine()
                {
                    LineNumber = lineNumber,
                    InterimTokens = dstList.ToArray()
                });
            }
            else
            {
                // replace or remove line
                if (dstList.All(c => c is EOLWonbeInterToken))
                {
                    // remove line
                    StoredSource.Remove(foundLine);
                }
                else
                {
                    // replace line
                    foundLine.InterimTokens = dstList.ToArray();
                }
            }
        }

        private async Task editLine(bool hasLineNumber, int lineNumber, List<WonbeInterToken> dstList)
        {
            await editLineWithoutSort(hasLineNumber, lineNumber, dstList);
            sortSourceLines();
        }

        /* プログラムの実行開始 */
        void do_run()
        {
            clearRuntimeInfo();
            gotoInterpreterMode();
            intermeditateExecutionPointer = 0;
        }

        void do_new()
        {
            // プログラムと実行環境をリセット
            clearRuntimeInfo();
            gotoInteractiveMode();
            traceFlag = false;
        }

        private async Task<bool> loadSource(string p)
        {
            do_new();
            var reader = new StringReader(p);
            var list = new List<WonbeInterToken>();
            await interactiveMainAsync(list);
            this.intermeditateExecitionLine = list.ToArray();
            clearRuntimeInfo();
            if (bForceToReturnSuper) return false; else return true;
        }

        public static string GetMyName()
        {
            return "Wonbe 2019 Ver " + myVersion;
        }

        internal async Task SuperMain(bool runRequest, string sourceCodeFileName)
        {
            if (runRequest)
            {
                if (await loadSource(sourceCodeFileName))
                {
                    do_run();
                    gotoInterpreterMode();
                }
            }
            for (; ; )
            {
                if (bForceToExit) return;
                clearModes();
                if (bInteractive)
                    await interactiveMainAsync(new List<WonbeInterToken>());
                else
                    await interpreterMain();
            }
        }
        public Wonbe(LanguageBaseEnvironmentInfo Environment) => this.Environment = Environment;
    }
}
