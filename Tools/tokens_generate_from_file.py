#!/usr/bin/env python

"""
This script uses CPython built-in `tokenize` module for creating tokens
from specified source file and dumps in json for using in Tokenizer tests.
"""

import argparse
import json
import sys
from pathlib import Path
from token import tok_name
from tokenize import TokenInfo, generate_tokens

parser = argparse.ArgumentParser(
    sys.argv[0], description="Generator of the test data from CPython tokenizer."
)
parser.add_argument("file", nargs=1, help="input python file for tokenizing")
parser.add_argument("-o", "--output", help="output JSON-file", required=True)
parser.add_argument(
    "-i",
    "--indents",
    help="indentation in the output file",
    type=int,
    metavar="int",
    default=None,
)
args = parser.parse_args(sys.argv[1:])


def gen_data(path: Path):
    with open(path) as f:
        lines = f.readlines()

    lines_it = iter(lines)

    def my_readline():
        try:
            return next(lines_it)
        except StopIteration:
            return ""

    tokens = generate_tokens(my_readline)
    tokens = [construct_token_dict(tok) for tok in tokens]

    data = {"Name": path.stem, "Tokens": tokens}

    return data


def construct_token_dict(token: TokenInfo):
    string = token.string
    type = get_type_str(token.exact_type)

    start = {"Line": token.start[0], "Column": token.start[1]}
    end = {"Line": token.end[0], "Column": token.end[1]}

    return {"Lexeme": string, "Type": type, "Start": start, "End": end}


token_mapping = {
    "ENDMARKER": "EndOfFile",
    "NAME": "Name",
    "NUMBER": "Number",
    "STRING": "StringLiteral",
    "NEWLINE": "NewLine",
    "INDENT": "Indent",
    "DEDENT": "Dedent",
    "LPAR": "LeftParen",
    "RPAR": "RightParen",
    "LSQB": "LeftSquareBracket",
    "RSQB": "RightSquareBracket",
    "COLON": "Colon",
    "COMMA": "Comma",
    "SEMI": "Semicolon",
    "PLUS": "Plus",
    "MINUS": "Minus",
    "STAR": "Star",
    "SLASH": "Slash",
    "VBAR": "VertBar",
    "AMPER": "Ampersand",
    "LESS": "Less",
    "GREATER": "Greater",
    "EQUAL": "Equal",
    "DOT": "Dot",
    "PERCENT": "Percent",
    "LBRACE": "LeftBrace",
    "RBRACE": "RightBrace",
    "EQEQUAL": "EqEqual",
    "NOTEQUAL": "NotEqual",
    "LESSEQUAL": "LessEqual",
    "GREATEREQUAL": "GreaterEqual",
    "TILDE": "Tilde",
    "CIRCUMFLEX": "Circumflex",
    "LEFTSHIFT": "LeftShift",
    "RIGHTSHIFT": "RightShift",
    "DOUBLESTAR": "DoubleStar",
    "PLUSEQUAL": "PlusEqual",
    "MINEQUAL": "MinusEqual",
    "STAREQUAL": "StarEqual",
    "SLASHEQUAL": "SlashEqual",
    "PERCENTEQUAL": "PercentEqual",
    "AMPEREQUAL": "AmpersandEqual",
    "VBAREQUAL": "VertBarEqual",
    "CIRCUMFLEXEQUAL": "CircumflexEqual",
    "LEFTSHIFTEQUAL": "LeftShiftEqual",
    "RIGHTSHIFTEQUAL": "RightShiftEqual",
    "DOUBLESTAREQUAL": "DoubleStarEqual",
    "DOUBLESLASH": "DoubleSlash",
    "DOUBLESLASHEQUAL": "DoubleSlashEqual",
    "AT": "At",
    "ATEQUAL": "AtEqual",
    "RARROW": "RightArrow",
    "ELLIPSIS": "Ellipsis",
    "COLONEQUAL": "ColonEqual",
    "EXCLAMATION": "Exclamation",
    "FSTRING_START": "FStringStart",
    "FSTRING_MIDDLE": "FStringMiddle",
    "FSTRING_END": "FStringEnd",
    "TSTRING_START": "TStringStart",
    "TSTRING_MIDDLE": "TStringMiddle",
    "TSTRING_END": "TStringEnd",
    "COMMENT": "Comment",
    "NL": "TriviaNewLine",
    "ERRORTOKEN": "Error",
}


def get_type_str(type: int):
    return token_mapping[tok_name[type]]


if __name__ == "__main__":
    out = Path(args.output)
    file = Path(args.file[0])
    indents = args.indents

    data = gen_data(file)

    with open(out, "w+") as f:
        json.dump(
            data,
            f,
            indent=indents,
            separators=(",", ":") if indents is None else (",", ": "),
        )
