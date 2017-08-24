﻿using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Jhu.Graywulf.ParserLib.Test
{
    [Grammar(Namespace = "TestNS", RootToken = "TestNS.List")]
    class TestGrammar : Grammar
    {
        public static Expression<Symbol> Comma = () => @",";

        public static Expression<Whitespace> Whitespace = () => @"\G\s+";
        public static Expression<Comment> SingleLineComment = () => @"\G//.*";
        public static Expression<Comment> MultiLineComment = () => @"\G(?sm)/\*.*?\*/!";
        public static Expression<Terminal> Word = () => @"\G([a-zA-Z_]+[a-zA-Z0-9_]*)";

        public static Expression<Rule> CommentOrWhitespace = () =>
            Sequence
            (
                Must(MultiLineComment, SingleLineComment, Whitespace),
                May(CommentOrWhitespace)
            );

        public static Expression<Rule> Argument = () => Word;

        public static Expression<Rule> List = () =>
            Sequence
            (
                May(CommentOrWhitespace),
                Word,
                May
                (
                    Sequence
                    (
                        May(CommentOrWhitespace),
                        Comma,
                        List
                    )
                )
            );

        public static Expression<Rule> List1 = () => Inherit(List);

        public static Expression<Rule> BaseRule1 = () =>
            Sequence
            (
                List
            );

        public static Expression<Rule> BaseRule2 = () =>
            Sequence
            (
                BaseRule3
            );

        public static Expression<Rule> BaseRule3 = () =>
            Sequence
            (
                Word, Comma, Word, BaseRule4
            );

        public static Expression<Rule> BaseRule4 = () =>
            Sequence
            (
                Word, Comma, Word
            );

        public static Expression<Rule> BaseRule5 = () =>
            Sequence
            (
                Word, Comma, Word, BaseRule6
            );

        public static Expression<Rule> BaseRule6 = () =>
            Sequence
            (
                Word, Comma, Word
            );
    }
}
