connect / as sysdba
alter session set container=pdborcl;
create user bimrl identified by bimrl quota unlimited on users;
grant create session, create table, create public synonym, create type, create trigger, create sequence, create view to bimrl;
REM **
REM *** to create additional user the following lines can be used
REM create user newuser identified by newuser quota unlimited on users;
REM grant create session, create table, create type, create trigger, create sequence, create view to bimrl;
REM **
connect bimrl/bimrl@pdborcl
REM **
REM * Create BIMRL dictionary tables. This is needed only once
REM **
@BIMRL-std-once.sql
REM **
REM * Insert color dictionary into ColorDict table
REM **
@colorDict_ins.sql
REM **
REM * Insert IFC object hierarchy into dictionary
REM * (Not needed for this purpose)
REM **
REM @objhier_ins.sql
REM **
