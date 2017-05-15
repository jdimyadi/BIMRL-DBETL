/*
// BIMRL (BIM Rule Language) library: this library performs DSL for rule checking using BIM Rule Language that works on BIMRL Simplified Schema on RDBMS. 
// This work is part of the original author's Ph.D. thesis work on the automated rule checking in Georgia Institute of Technology
// Copyright (C) 2013 Wawan Solihin (borobudurws@hotmail.com)
// 
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 3 of the License, or any later version.
//
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this library; If not, see <http://www.gnu.org/licenses/>.
*/

grammar bimrl;

 @lexer::members
 {
	public static int WHITESPACE = 1;
	public static int COMMENTS = 2;
 }

/*
 * Parser Rules
 */
test_rule:			TEST ( assignment_stmt
							| show_stmt
							| reset_all_var
							| expr
							| color_spec
							| function
						)
					| start_rule
					| test_rule ';' test_rule ';'
					;

start_rule:			bimrl_rule ( bimrl_rule )*;

bimrl_rule:			assignment_stmt ';'
					| show_stmt ';'
					| reset_all_var ';'
					| (bimrl_triplets)+
					| sql_stmt ';'
					| delete_var ';'
					| delete_model ';'
					;

bimrl_triplets:		check_stmt evaluate_stmt? action_stmt? ;

show_stmt:			SHOW VARS
					;

reset_all_var:		RESET VARS
					;

delete_var:			DELETE VARNAME
					;

assignment_stmt: SETVAR varname assign value 
					| SETVAR varnamelist select_expr
					;

assign:				':=';

select_expr:		select_stmt
					| '(' select_expr ')'
					| select_expr select_boolean select_expr
					;

select_stmt:		SELECT ( UNIQUE | DISTINCT | ALL )? column_list FROM table_list ( where_clause )? ;	

select_boolean:		UNION (ALL)?
					| INTERSECT
					| MINUS
					;

where_clause:		WHERE expr ;

group_clause:		GROUP BY simple_id_list
					| ORDER BY simple_id_list (ASCENDING | DESCENDING)?
					;

sql_stmt:			SQLANYTHING ;  // For SQL statement, we will just pass the statement through without changing anything, except renaming the table name with hex id suffix

varname:			VARNAME ;

varnamelist:		varname ( ',' varname )*
					| '(' varname ( ',' varname )* ')' ;

delete_model:		DELETE MODEL '(' ( FEDID '=' INT | project_name_number ',' project_name_number) ')' ;

project_name_number: PROJECTNAME '=' stringliteral
						| PROJECTNUMBER '=' stringliteral
						;

check_stmt:			CHECK single_check_stmt ';'
					| CHECK ( multi_check_stmt ';' )+
					;

single_check_stmt:	id_list ( where_clause )? (collect_stmt)? ;

collect_stmt:		COLLECT id_list (group_clause)*;

multi_check_stmt:	'{' single_check_stmt '}' AS setname ;

evaluate_stmt:		EVALUATE expr ( output )? (FOREACH GROUP OF (AGGREGATE)? '(' simple_id_list ')' )? (FROM set_clause)? (join_clause)? construct? ';'
					| EVALUATE ( foreach_fnexpr )+
					;

foreach_fnexpr:		'{' expr output (FOREACH GROUP OF (AGGREGATE)? '(' simple_id_list ')' )? (FROM set_clause)? (join_clause)? construct? '}' ';' ;

output:				OUTPUT varname;

join_clause:		( LEFT | RIGHT | FULL )? ( OUTER )? JOIN set_clause ( ON '(' id_dot '=' id_dot (AND id_dot '=' id_dot)* ')' | USING '(' id (',' id)* ')' ); 

set_clause:			setname
					| '(' select_stmt ')' ;

action_stmt:		ACTION (( one_action ';') | ( multi_action ';' )+) ;

multi_action:		when_clause '{' one_action '}' ;

one_action:			print_action (draw_action)?
					| draw_action (print_action)? ;

print_action:		PRINT (RESULT | simple_id_list)? (save_action)?;

draw_action:		DRAW (RESULT)? (COLOR color_spec)? (background_model)? (highlight_object)? save_action ;

highlight_object:	HIGHLIGHT '(' (simple_id_list | value_list) ')' (COLOR color_spec)? ;

background_model:	WITH ( transparent )? BACKGROUND id_list (where_clause)? ;

transparent:		(NUMBER)? TRANSPARENT ;

save_action:		SAVE INTO save_destination ( AND save_destination )? ;

save_destination:	X3D x3dfilename
					| TABLE table_name (APPEND)?
					| JSON jsonfilename
					;

bcffilename:		stringliteral;
x3dfilename:		stringliteral;
jsonfilename:		stringliteral;

value_list:			value ( ',' value )* ;

construct:			CONSTRUCT alias '(' geometry_type ')' ;

geometry_type:		LINE '(' ( alias | three_position) (',' (alias | three_position))+ ')' 
					| LINE '(' alias (offset)? ',' alias (offset)? ')' 
					| BOX '(' three_position three_position ')'
					| EXTRUSION '(' face_spec ',' direction ',' extrusion ')'
					| BREP '(' VERTICES '(' three_position (',' three_position)* ')' ',' face_indexes (',' noVertInFace)? ')'
					| BREP '(' STARTENDFACES '(' face_spec ',' face_spec ')' ')'
					| BREP '(' FACESET '(' (face_spec)+ ')' ')'
					| BREPFROMEDGE '(' face_spec ',' depth ',' extrusion (',' segmentize)? ')'
					;

direction:			sign? (XAXIS | YAXIS | ZAXIS | normal | VECTOR three_position )	;

three_position:		'(' signed_number ',' signed_number ',' signed_number ')' ;

face_indexes:		FACEINDEXES '(' INT (',' INT)* ')' ;

face_spec:			DEFFACE '(' (alias | (three_position (',' three_position)+)) ')' offset? (extend)? 
					| DEFPOINT ('(' alias ')' | three_position ) offset? ;

depth:				signed_number;

extrusion:			signed_number | arithmetic_expr ;

arithmetic_expr:	alias arithmetic_ops alias;

extend:				EXTEND signed_number ( XEDGE | YEDGE | BOTHDIRECTION )?;

segmentize:			NUMBER ;

offset:				OFFSET (three_position | ('(' alias ',' signed_number ')') | ('(' normal ',' signed_number ')') ) ;

normal:				NORMAL '(' alias ')';

noVertInFace:		INTEGER;

when_clause:		WHEN expr;

table_list:			id_list ;

column_list:		id_list | all_columns ;

all_columns:		'*' ;

table_name:			id ;

function_name:		id ;

type_name:			id ;

setname:			id ;

property:			id_dot ;

alias:				id ;

ext_id_dot_notation: id ('.' id)*
					| (id '.')? function ('.' property)? 
					;

id_dot:				id ('.' id)* ;

simple_id_list:		id_dot (',' id_dot)*
					;

function:			function_name '(' ( (UNIQUE)? expr ( ',' expr )* | '*' | ) ')' ;

id_list:			id_member (',' id_member)* ;

id_array:			'(' id (',' id )* ')' ;

id_member:			(id_array | stringliteral | ext_id_dot_notation) (alias)? ;

id:					STRINGDOUBLEQUOTE
					| ID
  					;

pattern:			STRING ;

value:				realliteral
					| stringliteral
					| BOOLEAN
					| NULL;

stringliteral:		STRING ;

realliteral:		signed_number ;

color_spec:			( RGB '(' NUMBER ',' NUMBER ',' NUMBER ')'
					| RED
					| GREEN
					| BLUE
					| CYAN
					| MAGENTA
					| YELLOW
					| WHITE
					| BLACK ) (transparency)?
					;

transparency:		TRANSPARENCY NUMBER;

expr:				value
					| ext_id_dot_notation
					| VARNAME
					| BINDNAME
					| unary_operator expr
					| expr ops expr
					| '(' expr ')'
					| varname_with_bind
					| expr conditional_expr
					| exists
					;

ops:				arithmetic_ops
					| comparison_ops
					| logical_ops
					;
		
arithmetic_ops:		MULTIPLY | DIVIDE | ADDITION | SUBTRACT ;

comparison_ops:		LT | LE | GT | GE | EQ | EQ_DBL | NOTEQ | NOTEQ2 | LIKE | NOT LIKE | REGEXP_LIKE | NOT REGEXP_LIKE ;

logical_ops:		AND | OR ;

unary_operator:		'-'
					| '+'
					| NOT
					;

varname_with_bind:	VARNAME BIND ( expr | '(' expr ( ',' expr )* ')' ) ;

conditional_expr:	null_condition
					| between_condition
					| in_condition
					;

null_condition:		IS NOT? NULL ;

between_condition:  NOT? BETWEEN expr AND expr ;

in_condition:		NOT? IN '(' ( select_expr | expr ( ',' expr )* ) ')' ;

exists:				NOT? EXISTS '(' select_expr ')' ;

/*
 * Lexer rules
*/

// Command tokens

TEST:			[Tt][Ee][Ss][Tt] ;

ACTION:			[Aa][Cc][Tt][Ii][Oo][Nn] ;
APPEND:			[Aa][Pp][Pp][Ee][Nn][Dd] ;
CHECK:			[Cc][Hh][Ee][Cc][Kk] ;
COLLECT:		[Cc][Oo][Ll][Ll][Ee][Cc][Tt] ;
CONSTRUCT:		[Cc][Oo][Nn][Ss][Tt][Rr][Uu][Cc][Tt] ;
CREATE:			[Cc][Rr][Ee][Aa][Tt][Ee] ;
DELETE:			[Dd][Ee][Ll][Ee][Tt][Ee] ;
DEFINE:			[Dd][Ee][Ff][Ii][Nn][Ee] ;
DRAW:			[Dd][Rr][Aa][Ww] ;
EVALUATE:		[Ee][Vv][Aa][Ll][Uu][Aa][Tt][Ee] ;
FROM:			[Ff][Rr][Oo][Mm] ;
HIGHLIGHT:		[Hh][Ii][Gg][Hh][Ll][Ii][Gg][Hh][Tt] ;
INSERT:			[Ii][Nn][Ss][Ee][Rr][Tt] ;
PRINT:			[Pp][Rr][Ii][Nn][Tt] ;
RESET:			[Rr][Ee][Ss][Ee][Tt] ;
SAVE:			[Ss][Aa][Vv][Ee] ;
SELECT:			[Ss][Ee][Ll][Ee][Cc][Tt] ;
SETVAR:			[Ss][Ee][Tt][Vv][Aa][Rr] ;
SHOW:			[Ss][Hh][Oo][Ww] ;
SQL:			[Ss][Qq][Ll] ;
UPDATE:			[Uu][Pp][Dd][Aa][Tt][Ee] ;
WHEN:			[Ww][Hh][Ee][Nn] ;
WHERE:			[Ww][Hh][Ee][Rr][Ee] ;

// Keywords:
AGGREGATE:		[Aa][Gg][Gg][Rr][Ee][Gg][Aa][Tt][Ee] ;
ALIGN:			[Aa][Ll][Ii][Gg][Nn] ;
ALL:			[Aa][Ll][Ll] ;
AT:				[Aa][Tt] ;
AS:				[Aa][Ss] ;
ASCENDING:		[Aa][Ss][Cc]([Ee][Nn][Dd][Ii][Nn][Gg])? ;
BACKGROUND:		[Bb][Aa][Cc][Kk][Gg][Rr][Oo][Uu][Nn][Dd] ;
BCFFILE:		[Bb][Cc][Ff][Ff][Ii][Ll][Ee] ;
BETWEEN:		[Bb][Ee][Tt][Ww][Ee][Ee][Nn] ;
BIND:			[Bb][Ii][Nn][Dd] ;
BLACK:			[Bb][Ll][Aa][Cc][Kk] ;
BLUE:			[Bb][Ll][Uu][Ee] ;
BOOLEANTYPE:	[Bb][Oo][Oo][Ll][Ee][Aa][Nn] ;
BREP:			[Bb][Rr][Ee][Pp] ;
BREPFROMEDGE:	[Bb][Rr][Ee][Pp][Ff][Rr][Oo][Mm][Ee][Dd][Gg][Ee] ;
BOTHDIRECTION:	[Bb][Oo][Tt][Hh][Dd][Ii][Rr][Ee][Cc][Tt][Ii][Oo][Nn] ;
BOX:			[Bb][Oo][Xx] ;
BY:				[Bb][Yy] ;
CYAN:			[Cc][Yy][Aa][Nn] ;
COLOR:			[Cc][Oo][Ll][Oo][Rr] ;
DEFFACE:		[Dd][Ee][Ff][Ff][Aa][Cc][Ee] ;
DEFFACEFROMEDGE:[Dd][Ee][Ff][Ff][Aa][Cc][Ee][Ff][Rr][Oo][Mm][Ee][Dd][Gg][Ee] ;
DEFPOINT:		[Dd][Ee][Ff][Pp][Oo][Ii][Nn][Tt] ;
DESCENDING:		[Dd][Ee][Ss][Cc]([Ee][Nn][Dd][Ii][Nn][Gg])? ;
DISTINCT:		[Dd][Ii][Ss][Tt][Ii][Nn][Cc][Tt] ;
DOUBLETYPE:		[Dd][Oo][Uu][Bb][Ll][Ee] ;
ELEMENTSET:		[Ee][Ll][Ee][Mm][Ee][Nn][Tt][Ss][Ee][Tt] ;
EXISTS:			[Ee][Xx][Ii][Ss][Tt][Ss] ;
EXTEND:			[Ee][Xx][Tt][Ee][Nn][Dd] ;
EXTRUSION:		[Ee][Xx][Tt][Rr][Uu][Ss][Ii][Oo][Nn] ;
FACEINDEXES:	[Ff][Aa][Cc][Ee][Ii][Nn][Dd][Ee][Xx][Ee][Ss] ;  
FACESET:		[Ff][Aa][Cc][Ee][Ss][Ee][Tt] ;
FEDID:			[Ff][Ee][Dd][Ii][Dd] ;
FOREACH:		[Ff][Oo][Rr][Ee][Aa][Cc][Hh] ;
FULL:			[Ff][Uu][Ll][Ll] ;
GEOMETRY:		[Gg][Ee][Oo][Mm][Ee][Tt][Rr][Yy] ;
GREEN:			[Gg][Rr][Ee][Ee][Nn] ;
GROUP:			[Gg][Rr][Oo][Uu][Pp] ;
IN:				[Ii][Nn] ;
INTERSECT:		[Ii][Nn][Tt][Ee][Rr][Ss][Ee][Cc][Tt] ;
INTEGERTYPE:	[Ii][Nn][Tt][Ee][Gg][Ee][Rr] ;
INTO:			[Ii][Nn][Tt][Oo] ;
IS:				[Ii][Ss] ;
JOIN:			[Jj][Oo][Ii][Nn] ;
JSON:			[Jj][Ss][Oo][Nn] ;
LEFT:			[Ll][Ee][Ff][Tt] ;
LINE:			[Ll][Ii][Nn][Ee] ;
MAGENTA:		[Mm][Aa][Gg][Ee][Nn][Tt][Aa] ;
MINUS:			[Mm][Ii][Nn][Uu][Ss] ;
MODEL:			[Mm][Oo][Dd][Ee][Ll] ;
NORMAL:			[Nn][Oo][Rr][Mm][Aa][Ll] ;
NOT:			[Nn][Oo][Tt] ;
NULL:			[Nn][Uu][Ll][Ll] ;
OF:				[Oo][Ff] ;
OFFSET:			[Oo][Ff][Ss][Ee][Tt] ;
ON:				[Oo][Nn] ;
ORDER:			[Oo][Rr][Dd][Ee][Rr] ;
OUTER:			[Oo][Uu][Tt][Ee][Rr] ;
OUTPUT:			[Oo][Uu][Tt][Pp][Uu][Tt] ;
PLACE:			[Pp][Ll][Aa][Cc][Ee] ;
PROJECTNAME:	[Pp][Rr][Oo][Jj][Ee][Cc][Tt][Nn][Aa][Mm][Ee] ;
PROJECTNUMBER:	[Pp][Rr][Oo][Jj][Ee][Cc][Tt][Nn][Uu][Mm][Bb][Ee][Rr] ;
RED:			[Rr][Ee][Dd] ;
RESULT:			[Rr][Ee][Ss][Uu][Ll][Tt] ;
RIGHT:			[Rr][Ii][Gg][Hh][Tt] ;
RGB:			[Rr][Gg][Bb] ;
START:			[Ss][Tt][Aa][Rr][Tt] ;
STARTENDFACES:	[Ss][Tt][Aa][Rr][Tt][Ee][Nn][Dd][Ff][Aa][Cc][Ee][Ss] ;
STRINGTYPE:		[Ss][Tt][Rr][Ii][Nn][Gg] ;
TABLE:			[Tt][Aa][Bb][Ll][Ee] ;
TO:				[Tt][Oo] ;
TRANSPARENCY:	[Tt][Rr][Aa][Nn][Ss][Pp][Aa][Rr][Ee][Nn][Cc][Yy] ;
TRANSPARENT:	[Tt][Rr][Aa][Nn][Ss][Pp][Aa][Rr][Ee][Nn][Tt] ;
UNION:			[Uu][Nn][Ii][Oo][Nn] ;
UNIQUE:			[Uu][Nn][Ii][Qq][Uu][Ee] ;
USING:			[Uu][Ss][Ii][Nn][Gg] ;
VALUES:			[Vv][Aa][Ll][Uu][Ee][Ss] ;
VARS:			[Vv][Aa][Rr][Ss] ;
VECTOR:			[Vv][Ee][Cc][Tt][Oo][Rr] ;
VERTICES:		[Vv][Ee][Rr][Tt][Ii][Cc][Ee][Ss] ;
WHITE:			[Ww][Hh][Ii][Tt][Ee] ;
WITH:			[Ww][Ii][Tt][Hh] ;
X3D:			[Xx][3][Dd] ;
XAXIS:			[Xx][Aa][Xx][Ii][Ss] ;
XEDGE:			[Xx][Ee][Dd][Gg][Ee] ;
YAXIS:			[Yy][Aa][Xx][Ii][Ss] ;
YEDGE:			[Yy][Ee][Dd][Gg][Ee] ;
YELLOW:			[Yy][Ee][Ll][Ll][Oo][Ww] ;
ZAXIS:			[Zz][Aa][Xx][Ii][Ss] ;   


/* Operators */
MULTIPLY:		'*';
DIVIDE:			'/';
ADDITION:		'+';
SUBTRACT:		'-';
LT:				'<';
LE:				'<=';
GT:				'>';
GE:				'>=';
EQ:				'=' ;
EQ_DBL:			'==' ;
NOTEQ:			'!=' ;
NOTEQ2:			'<>' ;
LIKE:			[Ll][Ii][Kk][Ee];
REGEXP_LIKE:	[Rr][Ee][Gg][Ee][Xx][Pp][_][Ll][Ii][Kk][Ee] ;
OR:				[Oo][Rr] | '||' ;
AND:			[Aa][Nn][Dd] | '&&';

// Variable tokens
VARNAME:		[?] ALPHANUMERIC+ ;
BINDNAME:		[:] ALPHANUMERIC+ ;
ID:				[a-zA-Z] ALPHANUMERIC* ;  // valid ID only starts with alphabet
STRING :		['] (ESC | .)*? ['] ;
STRINGDOUBLEQUOTE: '"' (ESC | .)*? '"' ;

fragment ALPHANUMERIC:	[a-zA-Z0-9_] ;
fragment ESC:			'\\' (["\\/bfnrt] | UNICODE) ;
fragment UNICODE :		'u' HEX HEX HEX HEX ;
fragment HEX :			[0-9a-fA-F] ;

sign:			'+' | '-' ;
signed_number:	( '+' | '-' )? NUMBER ;
signed_integer: ( '+' | '-' )? INTEGER ;

NUMBER:			INT '.' INT? EXP?   // 1.35, 1.35E-9, 0.3
				| '.' INT EXP?			// .2, .2e-9
				| INT EXP?            // 1e10
				| INT                // 45
				;

BOOLEAN :		[.][Tt][Rr][Uu][Ee][.]
				| [.][Tt][.]
				| [.][Ff][Aa][Ll][Ss][Ee][.]
				| [.][Ff][.]
				;

INCR:			'++'[0-9]* ;
DECR:			'--'[0-9]* ;
INTEGER:		INT ;
fragment INT:   [0] | [0-9] [0-9]* ; 
fragment EXP:   [Ee] [+\-]? INT ; 

PIPELINE:		'->' ;

SQLANYTHING:	[Ss][Qq][Ll] .*? ';' ;

WS:						[ \t\n\r]+ -> channel(WHITESPACE) ;

SINGLE_LINE_COMMENT:	'//' ~[\r\n]* -> channel(COMMENTS) ;

MULTILINE_COMMENT:		'/*' .*? ( '*/' | EOF ) -> channel(COMMENTS) ;

