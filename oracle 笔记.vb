56078535  nsd-it-service@fii-foxconn.com
oracle 異常處理
	1.  安裝：
		1>  centos7版本，報錯后修改以下文件內容：$ORACLE_HOME/sysman/lib/ins_emagent.mk 在$(MK_EMAGENT_NMECTL)后添加 -lnnz11 注意- 前有空格   
			sed -i 's/$(MK_EMAGENT_NMECTL)/$(MK_EMAGENT_NMECTL) -lnnz11/' ins_emagent.mk
		2>  安裝數據庫報錯，File "/etc/oratab" is not accessible ；
			sh /home/oracle/oraInventory/orainstRoot.sh sh /home/oracle/product/11.2.4/dbhome_1/root.sh
		3>  安裝要求： swap:16G / 目錄 150~200G /data 分區
            --                 
	2.  誤刪除表數據
		1>	dml 使用时间戳
			select * from SFIS1.C_ASY_BOM_T as of timestamp to_timestamp('2019-04-08 18:40:00','yyyy-mm-dd hh24:mi:ss')
		2>	drop table 使用recycle_bin 恢复
			一、如何查看是否开启回收站功能？
				SQL> show parameter recyclebin
				NAME         TYPE        VALUE
				----------- ----------- ----------
				recyclebin      string        on
				on：表示表空间启用的回收站功能，建议所有数据都开启这个功能，百利而无一害!
				备注：该参数可以设置成session级别打开，也可以设置成system级别，不用重启就可以生效
			二、如何不经过回收站直接删除并释放所占用空间？
				SQL> drop table cube_scope purge

				备注：此命令相当于truncate+drop操作，一般不建议这么操作！

			三、如何将回收站recyclebin中的对像还原？
				SQL> flashback table cube_scope to before drop
				表名可以是回收站系统的dba_recyclebin.object_name也可以是dba_recyclebin.original_name
				但是此时问题来了，我已经用备份的DDL语句重建了一个新的表，这个时候再用此命令还原显然会报错，这个时候怎么办呢，只能还原成一个别名，具体操作命令是
				SQL> flashback table cube_scope to before drop rename to cube_scope_old
				既然恢复了删除前的表中数据，现在只能从cube_scope_old中的数据插入cube_scope中
				SQL> insert into cube_scope select * from cube_scope_old t
				成功恢复了数据，是不是可以收工了？没有，还有什么忘记做了？想想？
				注意：如果将表drop掉，那么索引也被drop掉了，用这种方法把表找回来了，但是你的索引呢？你的约束呢？表恢复后一定要将表上的索引重建建立起来（切记），索引丢了最多影响性能，约束没了可能会造成业务数据混乱（一定要注意）
			四、如何手工清除回收站中的对像？
				SQL> purge table orabpel.cube_scope_old --清除具体的对像
				注意：如果此时是DBA用户操作其它用户数据，清除回收站中的表时要加上用户名，否则报表不在回收站中
				SQL> purge tablespace ORAPEL--清除指定的表空间对像
				SQL> purge tablespace ORAPEL user orabpel --删除表空间指定用户下的所有对像
				SQL> purge recyclebin--清空整个回收站
			五、show recyclebin为什么没有数据呢？
				首先们需要明白一点，recyclebin是user_recyclebin的同义词，如此你当前的登陆用户是system此时运用
				show recyclebin是没有数据据的
			六、如果同一对像多次删除怎么在recyclebin中识别？
				dba_recyclebin中对每删除一个对像都会以BIN$进行命名，同时会有相应的dropscn、createtime、droptime可以跟据这些对像进行定位，然后进行恢复
			七、ORACLE空间利用原则
				1. 使用现有的表空间的未使用空间
				2. 如果没有了空闲空间，则检查回收站，对于回收站的对象按照先进先出的原则，对于最先删除的对象，oracle在空间不足之时会最先从回收站删除以满足新分配空间的需求
				3. 如果回收站也没有对象可以清理，则检查表空间是否自扩展，如果自扩展则扩展表空间，然后分配新空间
				4.如果表空间非自扩展，或者已经不能自扩展(到达最大限制)，则直接报表空间不足错误，程序终止
			八、DROP掉的对像是不是都会经过回收站？
				以下几种drop不会将相关对像放进回收站recyclebin中
				* drop tablespace :会将recyclebin中所有属于该tablespace的对像清除
				* drop user ：会将recyclebin中所有属于该用户的对像清除
				* drop cluster : 会将recyclebin中所有属于该cluster的成员对像清除
				* drop type : 会将recyclebin中所有依赖该type对像清除
				另外还需要注意一种情况，对像所在的表空间要有足够的空间，不然就算drop掉经过recyclebin由于空间不足oracle会自动删除的哦（切记）！
	3.  關聯表update
		update MACHAO_TEST2 a set a.COURSE=(select b.EMP_NAME from MACHAO_TEST b where a.EMP_NO=b.EMP_NO)
	4.  Oracle 锁表与解锁表
		1>  查询被锁的会话ID： select session_id from v$locked_object;
		2>	查詢哪張表被鎖：
			SELECT B.OWNER, B.OBJECT_NAME, A.SESSION_ID, A.LOCKED_MODE
			FROM V$LOCKED_OBJECT A, DBA_OBJECTS B
			WHERE B.OBJECT_ID = A.OBJECT_ID;
		3>  查询上面会话的详细信息：SELECT sid, serial#, PADDR,username, osuser,MACHINE,PORT,PROGRAM,SQL_ADDRESS,SQL_ID FROM v$session  where sid = session_id ;
			SELECT sid, serial#, PADDR,username, osuser,MACHINE,PORT,PROGRAM FROM v$session;
		4>	查詢被鎖的會話所執行的sql: SELECT a.sid, a.serial#, a.PADDR,a.username, a.osuser,a.MACHINE，a.PORT,a.PROGRAM,b.SQL_FULLTEXT FROM v$session a v$sqlarea b where a.SQL_ID=b.SQL_ID a.sid = session_id ;
		5>  将上面锁定的会话关闭： ALTER SYSTEM KILL SESSION 'sid,serial#';   【select  'ALTER SYSTEM KILL SESSION '''||sid||','||serial#||''';' from v$session where username='MWEB'】
		6>  查看已經殺掉但是還沒有釋放的進程，注：該情況需要在操作系統kill
			select session_id from v$locked_object;
			select a.spid,b.sid,b.serial#,b.username from v$process a,v$session b where a.addr=b.paddr and b.status='KILLED';
		7>	根據PID查詢消耗內存的sql 查詢select s.sid,s.serial# from v$session s,v$process p where s.paddr=p.addr and p.spid='9999';

		8>	使用alter system kill 之後沒有釋放的session ,在os 繼續殺
			select a.SID,a.SERIAL#,a.STATUS,a.SERVER,a.PROCESS,a.PROGRAM,a.LOGON_TIME,b.PID,b.SPID,b.USERNAME,b.SERIAL# SERIAL,b.PROGRAM pgm,b.TRACEFILE FROM V$SESSION a left join V$process b on a.creator_addr=b.ADDR where a.STATUS='KILLED'
			select 'kill -9 '||b.SPID from  v$session a,v$process b where  a.status='INACTIVE' and a.PADDR=b.ADDR and a.username='AQMS_AP';
			select b.spid,a.sid,a.serial#,a.machine from v$session a,v$process b where a.paddr =b.addr  and a.sid = '3'
		9>	windows 下殺掉spid 
			cmd 下輸入orakill 實例名 SPID
			
	5.  修改安裝完 oracle 的 hostname 和 ip
		1>  修改host 文件 vi /etc/hosts
		2>  修改network 文件 (vi  /etc/sysconfig/network    NETWORKING=yes  HOSTNAME=XXX)
		2>  修改ip vi /etc/sysconfig/network-script/ifcfg-網卡 service network restart
	6.  REDO 文件損壞的處理以下幾種情況
		1>  損壞的是已歸檔且inactive,重建redo 即可,不會丟失數據
			alter database clear logfile group；
			alter database open;
		2>  損壞的是已歸檔且active或current 的redo 文件，在回復時，會丟失數據(已經commit 但是沒有寫到磁盤的數據會丟失)
			create pfile from spfile
			vi  pfile   添加 *._allow_resetlogs_corruption=TRUE
			create spfile from pfile
			startup mount
			recover database until cancel;
			cencel   
			alter database open resetlogs;
			select open_mode from v$database;			
	7.  share pool 爆滿的解決辦法:错误原因：共享内存太小，存在一定碎片，没有有效的利用保留区，造成无法分配合适的共享区。
		1>  查看当前环境
			SQL>show sga			　　
			Total System Global Area　566812832 bytes
			Fixed Size　　　　　　　　　　73888 bytes
			Variable Size　　　　　　　28811264 bytes
			Database Buffers　　　　　536870912 bytes
			Redo Buffers　　　　　　　　1056768 bytes
			
			SQL>show parameter shared_pool			
			NAME　　　　　　　　　　　　　　　　 TYPE　　VALUE
			------------------------------------ ------- -----
			shared_pool_reserved_size　　　　　　string　1048576
			shared_pool_size　　　　　　　　　　 string　20971520

			SQL> select sum(free_space) from v$shared_pool_reserved;			　
			SUM(FREE_SPACE)
			---------------
			　　1048576
			我们可以看到没有合理利用保留区
			
			SQL> SELECT SUM(RELOADS)/SUM(PINS) FROM V$LIBRARYCACHE;
			SUM(RELOADS)/SUM(PINS)
			----------------------
			　.008098188
			不算太严重
			
			SQL> SELECT round((B.Value/A.Value)*100,1) hardpaseperc
			FROM V$SYSSTAT A,V$SYSSTAT B
			WHERE A.Statistic# = 171 AND B.Statistic# = 172 AND ROWNUM = 1;　
			hardpaseperc
			------------------
			26.5　
		2>  查看保留区使用情况
			SQL>SELECT FREE_SPACE,FREE_COUNT,REQUEST_FAILURES,REQUEST_MISSES,LAST_FAILURE_SIZE FROM V$SHARED_POOL_RESERVED;
			FREE_SPACE FREE_COUNT REQUEST_FAILURES REQUEST_MISSES LAST_FAILURE_SIZE
			---------- ---------- ---------------- -------------- -----------------
			1048576　　　　　1　　　　　　　146　　　　　　　0　　　　　　　4132
			最近一次申请共享区失败时该对象需要的共享区大小4132　
			
			SQL>select name from v$db_object_cache where sharable_mem = 4132;
			name
			----------------
			dbms_lob
			-- dbms_lob正是exp时申请保留区的对象

		3>  查看导致换页的应用
			SQL> select * from x$ksmlru where ksmlrsiz>0;
			ADDR　　 INDX　　INST_ID KSMLRCOM　　　         KSMLRSIZ　KSMLRNUM       KSMLRHON             KSMLROHV KSMLRSES　
			50001A88  0　　　　　1    BAMIMA: Bam Buffer　    4100　　　　 64            DBMS_DDL              402745060 730DEB9C　　
			50001ACC  1　　　　　1    BAMIMA: Bam Buffer　    4108　　　　736            DBMS_SYS_SQL           1909768749 730D0838
			50001B10  2　　　　　1    BAMIMA: Bam Buffer　    4112　　　 1576            STANDARD              2679492315 730D7E20
			50001B54  3　　　　　1    BAMIMA: Bam Buffer    　4124　　　 1536            DBMS_LOB              853346312 730DA83C
			50001B98  4　　　　　1    BAMIMA: Bam Buffer　    4128　　　 3456            DBMS_UTILITY           4041615653 730C5FC8　
			50001BDC  5　　　　　1    BAMIMA: Bam Buffer　     4132　　　 3760           begin :1 := dbms_lob.getLeng...　2942875191 730CFFCC
			50001C20  6　　　　　1    state objects　　　         4184　　　 1088                                  0 00
			50001C64  7　　　　　1    library cache　　　         4192　　　　488                    EXU8VEW　                  2469165743 730C1C68
			50001CA8  8　　　　　1    state objects　　　         4196　　　　 16                                  0 730C0B90
			50001CEC  9　　　　　1    state objects　　　         4216　　　 3608                                   0 730D0838　

		4>  分析各共享池的使用情况
			SQL> select KSPPINM,KSPPSTVL from x$ksppi,x$ksppcv
			where x$ksppi.indx = x$ksppcv.indx and KSPPINM = '_shared_pool_reserved_min_alloc';　
			KSPPINM　　　　 KSPPSTVL
			-------------------------------　 --------
			_shared_pool_reserved_min_alloc　 4400　　--(门值)
			我们看到INDX=5,DBMS_LOB造成换页(就是做exp涉及到lob对象处理造成的换页情况),换出最近未使用的内存,但是换出内存并合并碎片后在共享区仍然没有合适区来存放数据,说明共享
			区小和碎片过多，然后根据_shared_pool_reserved_min_alloc的门值来申请保留区,而门值为4400,所以不符合申请保留区的条件,造成4031错误。我们前面看到保留区全部为空闲状态,所以我们可以
			减低门值，使更多申请共享内存比4400小的的对象能申请到保留区，而不造成4031错误。
		5>  解决办法：
			增大shared_pool （在不DOWN机的情况下不合适）
			打patch　 （在不DOWN机的情况下不合适）
			减小门值 （在不DOWN机的情况下不合适）
			
			因为LAST_FAILURE_SIZE<_shared_pool_reserved_min_alloc所以表明没有有效的使用保留区
			SQL> alter system set "_shared_pool_reserved_min_alloc" = 4000;
			alter system set "_shared_pool_reserved_min_alloc"=4000
			ERROR at line 1:
			ORA-02095: specified initialization parameter cannot be modified
			
			-- 9i的使用方法alter system set "_shared_pool_reserved_min_alloc"=4000 scope=spfile;
			使用alter system flush shared_pool; (不能根本性的解决问题)
			使用dbms_shared_pool.keep

		6>  由于数据库不能DOWN机，所以只能选择3)和4)
			运行dbmspool.sql
			SQL> @/home/oracle/products/8.1.7/rdbms/admin/dbmspool.sql
			找出需要keep到共享内存的对象
			SQL> select a.OWNER,a.name,a.sharable_mem,a.kept,a.EXECUTIONS ,b.address,b.hash_value
			from v$db_object_cache a,v$sqlarea b
			where a.kept = 'NO' and(( a.EXECUTIONS > 1000 and a.SHARABLE_MEM > 50000) or　a.EXECUTIONS > 10000) and SUBSTR(b.sql_text,1,50) = SUBSTR(a.name,1,50);
			OWNER　　NAME　　　　　　　　　　　　SHARABLE_MEM KEP EXECUTIONS ADDRESS　HASH_VALUE
			-------　----------------------—---　------------ --- ---------- -------- ----------
			SELECT COUNT(OBJECT_ID)　　 98292　　　　NO　 103207　　74814BF8 1893309624
			FROM ALL_OBJECTS
			WHERE OBJECT_NAME = :b1
			AND OWNER = :b2
			STANDARD　　　　　　　　　　286632　　　 NO　 13501
			DBMS_LOB　　　　　　　　　    　98292　 NO　 103750
			DBMS_LOB　　　　　　　     47536　　　　NO　 2886542
			DBMS_LOB　　　　　　　     11452　　　　NO　 2864757
			DBMS_PICKLER　　　　　　　　10684　　　　NO　 2681194
			DBMS_PICKLER　　　　　　　　5224　　　　 NO　 2663860
			SQL> execute dbms_shared_pool.keep('STANDARD');
			SQL> execute dbms_shared_pool.keep('74814BF8,1893309624','C');
			SQL> execute dbms_shared_pool.keep('DBMS_LOB');
			SQL> execute dbms_shared_pool.keep('DBMS_PICKLER');
			SQL> select OWNER, name, sharable_mem,kept,EXECUTIONS from v$db_object_cache where kept = 'YES' ORDER BY sharable_mem;
			SQL> alter system flush shared_pool;
			System altered.　　
			SQL> SELECT POOL,BYTES FROM V$SGASTAT WHERE NAME ='free memory';			　　
			POOL　　　　　　 BYTES
			----------- ----------
			shared pool　　7742756
			large pool　　　614400
			java pool　　　　32768
			[oracle@ali-solution oracle]$ sh /home/oracle/admin/dbexp.sh
			[oracle@ali-solution oracle]$ grep ORA- /tmp/exp.tmp
			未发现错误，导出数据成功　
			
			建议：
			由于以上解决的方法是在不能DOWN机的情况下，所以没能动态修改初始化参数，但问题的本质是共享区内存过小，需要增加shared pool，使用绑定变量，才能根本
			的解决问题，所以需要在适当的时候留出DOWN机时间，对内存进行合理的配置。
	8.	游標超過系統設定值，ORA-01000: 超出打开游标的最大数	
		1>  step 1: 查看数据库当前的游标数配置slqplus  /*+ rule*/
			show parameter open_cursors;
		2>  step 2:查看游标使用情况
			select o.sid, osuser, machine, count(*) num_curs from v$open_cursor o, v$session s where user_name = 'user' and o.sid=s.sid group by o.sid, osuser, machine order by  num_curs desc;
		3>  step 3:查看游标执行的sql情况
			select o.sid, q.sql_text from v$open_cursor o, v$sql q where q.hash_value=o.hash_value and o.sid = 123;
		4>  step 4:根据游标占用情况分析访问数据库的程序在资源释放上是否正常,如果程序释放资源没有问题，则加大游标数。
			alter system set open_cursors=2000 scope=both;

	9.	編譯存儲過程，顯示一直在執行中，查找被什麼進程佔用；
		1>  判斷 procedure 是否被鎖定；
			SELECT * FROM V$DB_OBJECT_CACHE WHERE name='PROC_BG_MATERIAL_BAK' AND LOCKS!='0';
		2>  查看是什麼session 佔用 procedure;		
			select /*+ rule*/ *  from V$ACCESS WHERE object='PROC_BG_MATERIAL_BAK';
		3>	查看session 情況；
			select * from v$session where SID=XX；
	10.	pl/sql 執行create/truncate 等操作報錯
		execute immediate 'truncate  table TABLE_NAME;
		authid current_user is
		由于甲方对数据库用户的严格限制，因此不会开放DBA权限，只会授予部分权限，如connect、resource权限，
		这就会导致跨用户之间的访问可能需要大量的授权语句。比如存储过程中的execute immediate语句，
		如果直接将execute immediate里面的语句拉出来访问，是可以访问的，但在存储过程中执行就会报出权限不足的错误，
		该解决方案是在存储过程的头部增加authid current_user is，该语句旨在给予其他用户（即非创建该存储过程的用户）使用该存储过程的权限。
	11.	分佈式報錯
		DBA_2PC_PENDING
		Oracle会自动处理分布事务，保证分布事务的一致性，所有站点全部提交或全部回滚。一般情况下，处理过程在很短的时间内完成，根本无法察觉到。
		但是，如果在commit或rollback的时候，出现了连接中断或某个数据库 站点CRASH的情况，则提交操作可能会无法继续，此时DBA_2PC_PENDING和DBA_2PC_NEIGHBORS中会包含尚未解决的分布事务。 对于绝大多数情况，当恢复连接或CRASH的数据库重新启动后，会自动解决分布式事务，不需要人工干预。只有分布事务锁住的对象急需被访问，锁住的回滚段阻止了其他事务的使用，网络故障或CRASH的数据库的恢复需要很长的时间等情况出现时，才使用人工操作的方式来维护分布式事务。 手工强制提交或回滚将失去二层提交的特性，Oracle无法继续保证事务的一致性，事务的一致性应由手工操作者保证
		使用ALTER SYSTEM DISABLE DISTRIBUTED RECOVERY，可以使Oracle不再自动解决分布事务，即使网络恢复连接或者CRASH的数据库重新启动。
		ALTER SYSTEM ENABLE DISTRIBUTED RECOVERY恢复自动解决分布事务。
		Oracle解决异布lock的方法！
		通常由于网络的不稳定或则数据库的 bug，在使用dblink时产生了异步lock，下面就谈谈异步lock的解决方法：
		1）查询 dba_2pc_pending ，确定异步lock当前的状态。
		select local_tran_id, global_tran_id, state, mix, host, commit#  fromdba_ 2pc_pending;


		LOCAL_TRAN_ID|GLOBAL_TRAN_ID        |STATE    |MIX|HOST      |COMMIT#
		-------------|----------------------|---------|---|----------|-------
		1.10.255     |V817REP.BE.ORACLE.COM.|committed|no |BE-ORACLE-|202241
					 |89f6eafb.1.10.255     |         |   |NT/bel449 |
		此时通过state=committed, 表示此session已提 交,只是在提交后，接受不到global session的transaction信息了,所以产生异步lock，此时对一般不造成table的lock。
		通过调用 execute DBMS_TRANSACTION.PURGE_LOST_DB_ENTRY('1.10.255'）； 可解决此问题。
		当然state还有以下几种状态：
		collecting:在收集数据过程中,产生异常
		解决方法： execute DBMS_TRANSACTION.PURGE_LOST_DB_ENTRY('1.10.255');

		prepared： 在接受到异步commit/rollback指令前， 产生异常
		解决方法： rollback force tran_id/commit force tran_id; -- 可根据异步transaction的状况决定使用方法。

		forced rollback： 在使用rollback force出现
		解决方法： execute DBMS_TRANSACTION.PURGE_LOST_DB_ENTRY('1.10.255');

		forced commit：在使用commit force出现
		解决方法： execute DBMS_TRANSACTION.PURGE_LOST_DB_ENTRY('1.10.255');

		** NOTE1: If using Oracle 9i or later and DBMS_TRANSACTION.PURGE_LOST_DB_ENTRY fails with
		   ORA-30019: Illegal rollback Segment operation in Automatic Undo mode, use the following workaround

		SQL> alter session set "_smu_debug_mode" = 4;
		SQL>execute DBMS_TRANSACTION.PURGE_LOST_DB_ENTRY('local_tran_id');

		测试 :
		模拟的是分布式事务在2pc提交过程产生in-doubt 的问题解决方式
		环境：orcl(ORCL01.TEST.COM),solo(ORCL02.TEST.COM) version 10.2.0.3
		15:45:29sys@SOLO > drop public database link solo_link;

		数据库链接已删除。

		15:45:57sys@SOLO >  create public database link solo_link connect to scott identified by scott using 'solo';

		数据库链接已创建。

		15:46:18sys@SOLO > updateemp@solo_link set sal=sal*2 ;

		15:46:38sys@SOLO > commit;
		如果这个时候solo出现网络故障。orcl执行commit 被挂起，这个时候如果网络恢复则问题会自动解决。
		而这时如果却执行了一个shutdown abort
		再启动之后,这个时候查询scott.emp表会报错：
		ERROR at line 1:
		ORA-01591: lock held by in-doubt distributed transaction 5.32.251

		这个时候查询dba_2pc_pending数据字典会看到5.32.251 的state是prepared
		并且同过查询dba_2pc_neighbors知道该事务对应的database是pu_link.test.com对应的数据库
		SQL> col local_tran_id format a13
		SQL> col global_tran_id format a30
		SQL> col state format a8
		SQL> col mixed format a3
		SQL> col host format a10
		SQL> col commit# format a10
		SQL> select local_tran_id, global_tran_id, state, mixed, host, commit#
		2 from dba_2pc_pending;
		LOCAL_TRAN_ID GLOBAL_TRAN_ID                            STATE       MIX   HOST       COMMIT#
		--------------------   ------------------------------------------     --------      ---    ----------   ----------
		5.32.251               ORCL.TEST.COM.8705ca3e.5.32.251  prepared         no    dg1          498537

		SQL> col local_tran_id format a13
		SQL> col in_out format a6
		SQL> col database format a25
		SQL> col dbuser_owner format a15
		SQL> col interface format a3
		SQL> select local_tran_id, in_out, database, dbuser_owner, interface
		2 from dba_2pc_neighbors;

		LOCAL_TRAN_ID IN_OUT DATABASE                  DBUSER_OWNER    INT
		--------------------    --------- -------------------------     ------------------          ---
		5.32.251             in                                                SYS                           N
		5.32.251               out         PU_LINK.TEST.COM     SYS                           C
		这时候就需要使用手动提交或回滚  commit或者rollback
		根据state列的值prepared我们知道，orcl是prepared阶段，则solo肯定不能到commit阶段.
		为了事务的一致性最好 rollback force '5.32.251';
					
		select local_tran_id, global_tran_id, state, mixed, host, commit#
		2 from dba_2pc_pending;

		LOCAL_TRAN_ID GLOBAL_TRAN_ID                            STATE         MIX HOST       COMMIT#
		-------------             -------------------------------------------       -----------     ---   ----------     ----------
		5.32.251               ORCL01.TEST.COM.8705ca3e.5.32. forced rollback  no    dg1
									 251                                                      
		DBMS_TRANSACTION.PURGE_LOST_DB_ENTRY('5.32.251');
oracle 啟動文件類型管理
    1.	oracle 啟動階段所用到的文件；
        startup nomount         -> 这个阶段会打开并读取配置文件，从配置文件中获取控制文件的位置信息
        alter database mount    -> 这个阶段会打开并读取控制文件，从控制文件中获取数据文件和联机重做日志文件的位置信息
        alter database open      -> 这个阶段会打开数据文件和联机重做日志文件
    2.  REDO 日誌管理
        1>  查看在線重做日誌
		    set linesize 300;
		    column MEMBER for a70;
		    column IS_RECOVERY_DEST_FILE for a25;
		    select * FROM v$logfile;
		    select * from v$log ;

        2>  變更redo 文件名稱或者是位置
			SQL> select member from v$logfile;
			SQL> shutdown immediate
			[oracle@ora1 ~]$ mv /u02/app/oracle/oradata/orcl/redo01.log  /u02/app/oracle/oradata/orcl/redo/redo01.log			
			SQL> startup mount
			SQL> alter database rename file '/u02/app/oracle/oradata/orcl/redo01.log' to '/u02/app/oracle/oradata/orcl/redo/redo01.log';
			SQL> alter database open
			SQL> select member from v$logfile;

        3>  添加redo 文件或者是刪除redo log或日誌組管理;
			<1> 增加日誌組
				alter database add logfile group 1('/data/oradata/CAILAMEI/onlinelog/online03.log') size 50M;
				alter database add logfile group 11('/data1/oradata/epodb/redo10.log') size 500M;
			<2> 刪除日誌組
				alter database drop logfile group 1;  -->刪除redo (注意 redo 至少要留兩個日誌組，redo 不可以直接刪除，要切歸檔到inactive )
			<3> 增加日誌組成員
				alter database add logfile member '/data/oradata/CAILAMEI/onlinelog/online04.log' to group 7; --如果日誌組當前使用，創建后的member 會invali''d;switch 會沒有問題
			<4> 刪除日誌組成員
				alter database drop logfile member '/data/oradata/CAILAMEI/onlinelog/online04.log';
			<5> 增加standbby 日誌組
				SQL> ALTER DATABASE ADD STANDBY LOGFILE GROUP 4 ('/u01/app/oracle/oradata/orcl/redo04.log') size 50M; 
			<6> 刪除 standby 日誌組
				ALTER DATABASE DROP STANDBY LOGFILE GROUP 4; 
			<7> 增加standby 日誌組成員
				alter database add STANDBY logfile member '/data/oradata/CAILAMEI/onlinelog/online10.log' to group 5;
			<8> 刪除standby 日誌組成員
				alter database  drop STANDBY logfile member '/data/oradata/CAILAMEI/onlinelog/online10.log';
				ALTER DATABASE 
			<9> 在線重命名日誌組成員
				首先更改物理路徑下的文件名稱	
			    alter database rename file '/data/oradata/CAILAMEI/onlinelog/o1_mf_3_h981oq81_.log' to '/data/oradata/CAILAMEI/onlinelog/redo03.log';
				alter database RENAME FILE '/diska/logs/log1a.rdo', '/diska/logs/log2a.rdo'   TO '/diskc/logs/log1c.rdo', '/diskc/logs/log2c.rdo'
    3.	歸檔日誌管理
        1>  啟用歸檔
			SQL> startup mount 
			SQL> alter database archivelog
			SQL> alter system set log_archive_dest_1='location=/data/arch' scope=both; (mount 和open和read only 狀態下均可創建歸檔)
        2>  切歸檔
			SQL>alter system switch logfile; (只能在open狀態下切歸檔)

        3>  關閉歸檔
			流程：shutdown immediate >startup mount > alter database noarchivelog > alter database open > archive log list;
        4>  刪除歸檔：
			RMAN> delete expired archivelog all;
			RMAN> delete archivelog all completed before 'sysdate-1';	
    4.	密碼文件管理
		1>	windows 環境
			C:/Documents and Settings/>orapwd file=D:/oracle/product/10.1.0/Db_1/database/PWDorcl.ORA password=admin entries=40 force=y; 
		2>	linux 環境
			orapwd file=$ORACLE_HOME/dbs/orapwCAILAMEI password=Foxconn123 entries=5 force=y;立即生效 password 是sys 的密碼		
    5.	控制文件
		1>	查看控制文件路徑
		SQL> show parameter control;
		或
		SQL> select name from v$controlfile;
		
		2>	模擬備源控制文件刪除；
		Thu May 28 08:31:31 2020
		ALTER DATABASE   MOUNT
		ORA-00210: cannot open the specified control file
		ORA-00202: control file: '/data/oradata/CAILAMEI/controlfile/o1_mf_gj670j41_.ctl'
		ORA-27037: unable to obtain file status
		Linux-x86_64 Error: 2: No such file or directory
		Additional information: 3
		ORA-205 signalled during: ALTER DATABASE   MOUNT...
		Thu May 28 08:31:32 
		
		Errors in file /home/oracle/diag/rdbms/cailamei_sty/CAILAMEI/trace/CAILAMEI_rfs_26486.trc:
		ORA-00367: checksum error in log file header
		ORA-00315: log 6 of thread 0, wrong thread # 1 in header
		ORA-00312: online log 6 thread 0: '/data/oradata/CAILAMEI/standbylog/standbyredo03.log'
		Clearing online log 4 of thread 0 sequence number 0
		Errors in file /home/oracle/diag/rdbms/cailamei_sty/CAILAMEI/trace/CAILAMEI_arc4_26472.trc:
		ORA-00367: checksum error in log file header
		ORA-00315: log 4 of thread 0, wrong thread # 1 in header
		ORA-00312: online log 4 thread 0: '/data/oradata/CAILAMEI/standbylog/standbyredo01.log'
		Clearing online log 5 of thread 0 sequence number 0
		Errors in file /home/oracle/diag/rdbms/cailamei_sty/CAILAMEI/trace/CAILAMEI_arc5_26474.trc:
		ORA-00367: checksum error in log file header
		ORA-00315: log 5 of thread 0, wrong thread # 1 in header
		ORA-00312: online log 5 thread 0: '/data/oradata/CAILAMEI/standbylog/standbyredo02.log'
		
		
		
oracle 工具類基本操作     
	1.  sqlplus工具	
		1>  sqlplus 基本命令
			show linesize : 查看当前设置的sqlplus输出的最大行宽
			set linesize : 设置sqlplus输出的最大行宽
			set pagesize：設置sqlplus 分頁，若不需要分頁，則 set pagesize 0;
			column : 修改显示字段的长度或名称
			column col_name format a15         将列col_name（字符型）显示最大宽度调整为15个字符
			column col_num format 999999      将列col_num（num型）显示最大宽度调整为6个字符
			column col_num heading col_num2   将col_num的列名显示为col_num2;
		2>	sqlplus 初始化參數文件及參數詳解
			$ORACLE_HOME/sqlplus/admin/glogin.sql
			set feedback on -- 顯示sql 語句查詢或者更新的行數
			set linesize 600
			set pagesize 600
			set name new_value gname  設定列的別名
			set sqlprompt "_user'@'_connect_identifier'> '" 
	2.  數據泵之導出/導入(expdp/impdp)
		1>  查看/創建目錄/授權
			select * from dba_directories;
			create directory exp_backup as '/data/expdata';    '/data/expbak';  
			grant read,write on directory  exp_backup to system; grant read,write on directory DUMP_DIR to system;
			修改路徑
			create or replace directory exp_backup as '/home/oracle/expdata';

		2>  導出:
			在Windows平台下，需要对象双引号进行转义，使用转义符\;
			在linux 環境下在未使用parfile文件的情形下，所有的符号都需要进行转义，包括括号，双引号，单引号等】基本命令
			expdp system/sys123sys directory=DUMP  dumpfile=netapp_%U.dmp logfile=netapp_20190621.log compression=all full=Y  PARALLEL=4
			expdp system/Foxconn123 directory=exp_backup  dumpfile=GDLSFCDB_%U.dmp logfile=fullbackup_20200608.log  full=Y compression=all
			壓縮導出： 
			compression=all/DATA_ONLY/METADATA_ONLY默認/NONE/DEFAULT(METADATA_ONLY/NONE/10g)
			導出表：
			windows 環境
			tables=用戶名.表名,用戶名.表名  QUERY = '表名1:"where deptno =20"','表名2:"where deptno <=20 and deptno >=10"'
			linux 環境
			tables=\'sfis1.AI_AP_CM602_INI\',\'sfis1.AI_AP_CM602_SET\' 
			導出 schema：
			expdp system/sys123sys directory=DUMP_DIR dumpfile=sfism4.dmp logfile=sfism4.log schemas=sfism4,sfis1
			導出對象定義/數據： 
			CONTENT={ALL | DATA_ONLY | METADATA_ONLY}
			控制执行任务的最大线程数：
			PARALLEL=4 FILESIZE=2M 配合%U 從1開始計數
			估算備份佔用空間大小不導出數據：
			ESTIMATE_ONLY={Y | N默認為導出數據} 配合估算方式 ESTIMATE={BLOCKS(默認大) | STATISTICS} 導出時不加dumpfile 且配合使用NOLOGFILE參數   
			排除對象導出： 
			【linux 環境】 EXCLUDE=SCHEMA:\"\IN \(\'SFISM4\'\)\"\
			【windows 環境】EXCLUDE=TABLE:"IN ('EMP','DEPT')",SEQUENCE,VIEW,INDEX:"= 'INDX_NAME'",PROCEDURE:"LIKE 'PROC_U%'(_代表任意字符),\"TABLE:\"> 'E' \"(大于字符E的所有表对象)  
			eg.
			
			expdp system/sys123sys@tjepd1big directory=DUMP_DIR dumpfile=epd1big_full_20190808.dmp logfile=epd1big_full_0808.log full=y  EXCLUDE=SCHEMA:\"IN \(\'SFISM4\'\)\"
			
			分區表轉普通表
			
			partion-option=merge
			導出表空間：expdp system/Foxconn123  directory=EXPBAKUP dumpfile=tablespace.dmp logfile=tablespace.log  tablespaces=MACHAO
			数据泵参数partition_options 在对于迁移分区表的使用。
			1NONE 象在系统上的分区表一样创建。
			2DEPARTITION 每个分区表和子分区表作为一个独立的表创建，名字使用表和分区（子分区）名字的组合。
			3MERGE 将所有分区合并到一个表

		3>  導入 
			REMAP_DATAFILE/SCHEMA/TABLESPACE 例如：remap_schema=scott:system
			TABLE_EXISTS_ACTION={SKIP | APPEND |TRUNCATE | FRPLACE } TABLE_EXISTS_ACTION=REPLACE
			impdp system/sys123sys  directory=DUMP  dumpfile=netapp_%U.dmp logfile=netapp_imp_20190622.log  full=Y  PARALLEL=4 resumabe=Y
		4>	中途kill 掉導入導出：
			select * from dba_datapump_jobs;
			attach进入交互状态，交互状态常用命令：
			status：查看作业状态，监控作业进度
			stop_job:停止任务
			start_job:启动恢复任务
			stop_job=IMMEDIATE 将立即关闭数据泵作业
			parallel=10 更改当前作业的活动 worker 的数目。
			KILL_JOB 分离和删除作业
		5>	使用parfile导出
			如果需要导出多个表的部分数据，那么如果用命令方式写，因为需要转译，是很麻烦的，也容易出错，这时我们可以使用parfile文件来避免大量的转译;
			首先看一下我们parfile.txt文件中的内容
			cat parfile.txt(parfile可以随意命名)
			tables=( CHANNEL.TR_CHANNEL_ACCESS_LOG_BAK, scott.test1, scott.test2)
			query=( xxxxx.TR_XXXX_ACCESS_LOG_BAK:"where CHANNEL_CODE='10689021144'", 
			scott.test1:"where UA_SERIAL_ID in ('96','26')", 
			scott.test2:"where FILESIZE=273899")
			
			export ORACLE_SID=XXXXX 
			expdp \'sys/********* as sysdba\' directory=sfdir dumpfile=export.dmp logfile=export.log parfile=/archlog/parfile.txt

			##我们可以把expdp命令需要使用的参数都写进参数文件中，类似如下：
			PARALLEL=3
			cluster=no 
			COMPRESSION=ALL
			DUMPFILE=export_%U.dmp  
			DIRECTORY=sfdir   
			logfile=export.log
			EXCLUDE=TRIGGER,
			INDEX,
			STATISTICS
			tables=( xxxxx.TR_xxxx_ACCESS_LOG_BAK, scott.test1, scott.test2)
			query=( xxxxx.TR_xxxx_ACCESS_LOG_BAK:"where CHANNEL_CODE='10689021144'", scott.test1:"where UA_SERIAL_ID in ('96','26')", scott.test2:"where FILESIZE=273899")

			然后执行如下命令
			expdp \'sys/*********** as sysdba\' parfile=/archlog/parfile.txt
	3.	RMAN 工具使用
		1>	RMAN中三个不完全恢复场景
			select systimestamp from dual;
			 run
				{
				set until time "to_date('2019-09-03:16:00:00','YYYY-MM-DD HH24:MI:SS')";
				restore database;
				recover database;
				}
				
	4.	oracle 日誌挖掘工具 LogMiner
		1)	oracle11g LogMiner 的使用方法
			1>	將歸檔日誌添加到LOGMNR
				exec dbms_logmnr.add_logfile(logfilename=>'/data/database/tjepd1db/arch/1_466410_785700410.dbf',options=>dbms_logmnr.new);
				exec dbms_logmnr.add_logfile(logfilename=>'/data/database/tjepd1db/arch/1_466411_785700410.dbf',options=>dbms_logmnr.addfile);
				select 'exec dbms_logmnr.add_logfile(logfilename=>''' ||name||''',options=>dbms_logmnr.addfile);'  from v$archived_log where name !='standbydb' and first_time >'2020-06-02 12:00:00' and first_time <'2020-06-02 12:10:00';

			2>	開始分析
				exec dbms_logmnr.start_logmnr(options=>dbms_logmnr.dict_from_online_catalog);
	 
			3>	查看LOGMNR分析後的數據。
				select timestamp,sql_redo from v$logmnr_contents;
	 
			4>	保存到table logmnr_contents
				create table logmnr_contents as select * from v$logmnr_contents;
	 
			5>	查看logmnr_contents內容
				select OPERATION,DATA_OBJ#,count(OPERATION) from sys.logmnr_contents group by OPERATION,DATA_OBJ#
	 
			6>	结束LOGMNR操作, drop table logmnr_contents
				exec dbms_logmnr.end_logmnr;
				drop table logmnr_contents PURGE;
		2)	oracle 8i/9i LogMiner 的使用方法
			LogMiner 包含兩個包，一個是dbms_logmnr_d(包括一個procedure dbms_logmnr_d.build()用於提取數據字典信息) 另一個是dbms_logmnr(add_logfile() /start_logmnr() /end_logmnr())
			提取日誌信息分為兩種，一種是使用字典文件，一種是不適用字典文件
			1>	使用字典文件
				startup mount；
				show pamameter utl; 
				alter system set utl_file_dir='/data/logmnr' scope=spfile; (-- 這個目錄用於存放提取出的數據字典信息)
				shutdown immediate;
				startup;
				exec dbms_logmnr_d.build(dictionary_filename =>'dic.ora',discionary_location =>'/data/logmnr'); discionary_location 需要和show pamameter utl 結果完全一致；
				或
				exec dbms_logmnr_d.build('dic.ora','/data/logmnr');
				exec dbms_logmnr.add_logfile(logfilename=>'/data/database/tjepd1db/arch/1_466410_785700410.dbf',options=>dbms_logmnr.new);
				exec dbms_logmnr.add_logfile(logfilename=>'/data/database/tjepd1db/arch/1_466411_785700410.dbf',options=>dbms_logmnr.addfile);
				exec dbms_logmnr.start_logmnr(dictfilename=>'/data/logmnr/dic.ora');
				select data_obj#,operation, count(1) from  v$logmnr_contents group by data_obj#,operation order by count(1) desc;
				exec dbms_logmnr.end_logmnr;
			2>	不使用字典文件(DB 不需要重啟)
				exec dbms_logmnr.add_logfile(logfilename=>'/data/database/tjepd1db/arch/1_466410_785700410.dbf',options=>dbms_logmnr.new);
				exec dbms_logmnr.add_logfile(logfilename=>'/data/database/tjepd1db/arch/1_466411_785700410.dbf',options=>dbms_logmnr.addfile);
				exec dbms_logmnr.start_logmnr();
				select data_obj#,operation, count(1) from  v$logmnr_contents group by data_obj#,operation order by count(1) desc;
				exec dbms_logmnr.end_logmnr();
				
				
				
				
			
			
	5.	固定sql执行计划
		1)	outline (不使用的话直接drop 掉outline 即可；10g 之前使用、8i 引入)
			[oracle@ora6 dbs]$ sqlplus / as sysdba
			SQL*Plus: Release 11.2.0.4.0 Production on Tue Dec 22 10:23:20 2020
			Copyright (c) 1982, 2013, Oracle.  All rights reserved.
			Connected to:
			Oracle Database 11g Enterprise Edition Release 11.2.0.4.0 - 64bit Production
			With the Partitioning, OLAP, Data Mining and Real Application Testing options

			SYS@test> conn cailamei
			Enter password:
			Connected.

			CAILAMEI@test> create table outline_test_20201222 as select * from dba_objects;
			Table created.

			CAILAMEI@test> create index outline_test_idx on outline_test_20201222(OBJECT_NAME);
			Index created.

			CAILAMEI@test> set autot on;
			CAILAMEI@test> select owner from outline_test_20201222 where OBJECT_NAME='OUTLINE_TEST_20201222';

			OWNER
			------------------------------
			CAILAMEI

			Execution Plan
			----------------------------------------------------------
			Plan hash value: 655338988

			-----------------------------------------------------------------------------------------------------
			| Id  | Operation                   | Name                  | Rows  | Bytes | Cost (%CPU)| Time     |
			-----------------------------------------------------------------------------------------------------
			|   0 | SELECT STATEMENT            |                       |     1 |    83 |     4   (0)| 00:00:01 |
			|   1 |  TABLE ACCESS BY INDEX ROWID| OUTLINE_TEST_20201222 |     1 |    83 |     4   (0)| 00:00:01 |
			|*  2 |   INDEX RANGE SCAN          | OUTLINE_TEST_IDX      |     1 |       |     3   (0)| 00:00:01 |
			-----------------------------------------------------------------------------------------------------

			Predicate Information (identified by operation id):
			---------------------------------------------------

			2 - access("OBJECT_NAME"='OUTLINE_TEST_20201222')

			Note
			-----
			- dynamic sampling used for this statement (level=2)


			Statistics
			----------------------------------------------------------
			10  recursive calls
			0  db block gets
			69  consistent gets
			0  physical reads
			0  redo size
			529  bytes sent via SQL*Net to client
			524  bytes received via SQL*Net from client
			2  SQL*Net roundtrips to/from client
			0  sorts (memory)
			0  sorts (disk)
			1  rows processed

			CAILAMEI@test> select /*+full(OUTLINE_TEST_20201222)*/owner from outline_test_20201222 where OBJECT_NAME='OUTLINE_TEST_20201222';

			OWNER
			------------------------------
			CAILAMEI


			Execution Plan
			----------------------------------------------------------
			Plan hash value: 3952155143

			-------------------------------------------------------------------------------------------
			| Id  | Operation         | Name                  | Rows  | Bytes | Cost (%CPU)| Time     |
			-------------------------------------------------------------------------------------------
			|   0 | SELECT STATEMENT  |                       |     1 |    83 |   345   (1)| 00:00:05 |
			|*  1 |  TABLE ACCESS FULL| OUTLINE_TEST_20201222 |     1 |    83 |   345   (1)| 00:00:05 |
			-------------------------------------------------------------------------------------------

			Predicate Information (identified by operation id):
			---------------------------------------------------

			1 - filter("OBJECT_NAME"='OUTLINE_TEST_20201222')

			Note
			-----
			- dynamic sampling used for this statement (level=2)


			Statistics
			----------------------------------------------------------
			10  recursive calls
			0  db block gets
			1302  consistent gets
			1234  physical reads
			0  redo size
			529  bytes sent via SQL*Net to client
			524  bytes received via SQL*Net from client
			2  SQL*Net roundtrips to/from client
			0  sorts (memory)
			0  sorts (disk)
			1  rows processed

			CAILAMEI@test> set autot off;
			CAILAMEI@test> create or replace outline 1_outline_test on select owner from outline_test_20201222 where OBJECT_NAME='OUTLINE_TEST_20201222';
			create or replace outline 1_outline_test on select owner from outline_test_20201222 where OBJECT_NAME='OUTLINE_TEST_20201222'                         *
			ERROR at line 1:
			ORA-18000: invalid outline name

			CAILAMEI@test> create or replace outline outline_test_1 on select owner from outline_test_20201222 where OBJECT_NAME='OUTLINE_TEST_20201222';
			Outline created.

			CAILAMEI@test> create or replace outline outline_test_2 on select /*+full(OUTLINE_TEST_20201222)*/owner from outline_test_20201222 where OBJECT_NAME='OUTLINE_TEST_20201222';
			Outline created.

			CAILAMEI@test> select name,owner,USED,SQL_TEXT from dba_outlines;

			NAME               OWNER         USED      SQL_TEXT
			----------------- --------------- --------- --------------------------------------------------------------------------------
			OUTLINE_TEST_1      CAILAMEI        UNUSED    select owner from outline_test_20201222 where OBJECT_NAME='OUTLINE_TEST_20201222
			OUTLINE_TEST_2      CAILAMEI        UNUSED    select /*+full(OUTLINE_TEST_20201222)*/owner from outline_test_20201222 where OB

			CAILAMEI@test> col name for a30;
			CAILAMEI@test> col OWNER for a20
			CAILAMEI@test> col HINT for a80
			CAILAMEI@test> set linesize 600

			CAILAMEI@test> select * from dba_outline_hints;
			NAME                           OWNER                      NODE      STAGE   JOIN_POS HINT
			------------------------------ -------------------- ---------- ---------- ---------- --------------------------------------------------------------------------------
			OUTLINE_TEST_1                 CAILAMEI                      1          1          1 INDEX_RS_ASC(@"SEL$1" "OUTLINE_TEST_20201222"@"SEL$1" ("OUTLINE_TEST_20201222"."
			OUTLINE_TEST_1                 CAILAMEI                      1          1          0 OUTLINE_LEAF(@"SEL$1")
			OUTLINE_TEST_1                 CAILAMEI                      1          1          0 ALL_ROWS
			OUTLINE_TEST_1                 CAILAMEI                      1          1          0 DB_VERSION('11.2.0.4')
			OUTLINE_TEST_1                 CAILAMEI                      1          1          0 OPTIMIZER_FEATURES_ENABLE('11.2.0.4')
			OUTLINE_TEST_1                 CAILAMEI                      1          1          0 IGNORE_OPTIM_EMBEDDED_HINTS
			OUTLINE_TEST_2                 CAILAMEI                      1          1          1 FULL(@"SEL$1" "OUTLINE_TEST_20201222"@"SEL$1")
			OUTLINE_TEST_2                 CAILAMEI                      1          1          0 OUTLINE_LEAF(@"SEL$1")
			OUTLINE_TEST_2                 CAILAMEI                      1          1          0 ALL_ROWS
			OUTLINE_TEST_2                 CAILAMEI                      1          1          0 DB_VERSION('11.2.0.4')
			OUTLINE_TEST_2                 CAILAMEI                      1          1          0 OPTIMIZER_FEATURES_ENABLE('11.2.0.4')

			NAME                           OWNER                      NODE      STAGE   JOIN_POS HINT
			------------------------------ -------------------- ----------
			---------- ---------- --------------------------------------------------------------------------------
			OUTLINE_TEST_2                 CAILAMEI                      1          1          0 IGNORE_OPTIM_EMBEDDED_HINTS

			12 rows selected.

			CAILAMEI@test> select * from dba_outline_hints where join_pos=1;

			NAME                           OWNER                      NODE      STAGE   JOIN_POS HINT
			------------------------------ -------------------- ---------- ---------- ---------- --------------------------------------------------------------------------------
			OUTLINE_TEST_1                 CAILAMEI                      1          1          1 INDEX_RS_ASC(@"SEL$1" "OUTLINE_TEST_20201222"@"SEL$1" ("OUTLINE_TEST_20201222"."
			OUTLINE_TEST_2                 CAILAMEI                      1          1          1 FULL(@"SEL$1" "OUTLINE_TEST_20201222"@"SEL$1")                                                                                                                                                                                                                            VARCHAR2(1000)

			CAILAMEI@test> update outln.OL$ set OL_NAME=decode(OL_NAME,'OUTLINE_TEST_1','OUTLINE_TEST_2','OUTLINE_TEST_2','OUTLINE_TEST_1') where OL_NAME in ('OUTLINE_TEST_1','OUTLINE_TEST_2');
			2 rows updated.
			CAILAMEI@test> commit;
			Commit complete.

			CAILAMEI@test> select name,owner,USED,SQL_TEXT from dba_outlines;

			NAME                           OWNER                USED   SQL_TEXT
			------------------------------ -------------------- ------ --------------------------------------------------------------------------------
			OUTLINE_TEST_2                 CAILAMEI             UNUSED select owner from outline_test_20201222 where OBJECT_NAME='OUTLINE_TEST_20201222
			OUTLINE_TEST_1                 CAILAMEI             UNUSED select /*+full(OUTLINE_TEST_20201222)*/owner from outline_test_20201222 where OB

			CAILAMEI@test> set autot on;
			CAILAMEI@test> select * from dba_outline_hints where join_pos=1;

			NAME                           OWNER                      NODE      STAGE   JOIN_POS HINT
			------------------------------ -------------------- ---------- ---------- ---------- --------------------------------------------------------------------------------
			OUTLINE_TEST_2                 CAILAMEI                      1          1          1 FULL(@"SEL$1" "OUTLINE_TEST_20201222"@"SEL$1")
			OUTLINE_TEST_1                 CAILAMEI                      1          1          1 INDEX_RS_ASC(@"SEL$1" "OUTLINE_TEST_20201222"@"SEL$1" ("OUTLINE_TEST_20201222"."

			CAILAMEI@test> select owner from outline_test_20201222 where OBJECT_NAME='OUTLINE_TEST_20201222';
			OWNER
			--------------------
			CAILAMEI

			Execution Plan
			----------------------------------------------------------
			Plan hash value: 3952155143

			-------------------------------------------------------------------------------------------
			| Id  | Operation         | Name                  | Rows  | Bytes | Cost (%CPU)| Time     |
			-------------------------------------------------------------------------------------------
			|   0 | SELECT STATEMENT  |                       |  1031 | 85573 |   345   (1)| 00:00:05 |
			|*  1 |  TABLE ACCESS FULL| OUTLINE_TEST_20201222 |  1031 | 85573 |   345   (1)| 00:00:05 |
			-------------------------------------------------------------------------------------------

			Predicate Information (identified by operation id):
			---------------------------------------------------

			1 - filter("OBJECT_NAME"='OUTLINE_TEST_20201222')

			Note
			-----
			- outline "OUTLINE_TEST_2" used for this statement


			Statistics
			----------------------------------------------------------
			59  recursive calls
			147  db block gets
			1310  consistent gets
			1234  physical reads
			624  redo size
			529  bytes sent via SQL*Net to client
			524  bytes received via SQL*Net from client
			2  SQL*Net roundtrips to/from client
			2  sorts (memory)
			0  sorts (disk)
			1  rows processed

		2)	sql_profile（10g 之后）
			1>	sql_profile 手动固定；
				explain plan for select * from OUTLINE_TEST_20201222 where OBJECT_NAME='OUTLINE_TEST_20201222';
				select * from table(dbms_xplan.display(null,null,'outline'));
				explain plan for select /*+ full(OUTLINE_TEST_20201222)*/* from OUTLINE_TEST_20201222 where OBJECT_NAME='OUTLINE_TEST_20201222';
				select * from table(dbms_xplan.display(null,null,'outline'));
				
				declare
				v_hints sys.sqlprof_attr;
				begin
				v_hints :=sys.sqlprof_attr('FULL(@"SEL$1" "OUTLINE_TEST_20201222"@"SEL$1")');
				dbms_sqltune.import_sql_profile(q'^select * from OUTLINE_TEST_20201222 where OBJECT_NAME='OUTLINE_TEST_20201222'^',v_hints,'OUTLINE_TEST_20201222_profile',force_match=>true);
				end;
				/			
				exec dbms_sqltune.drop_sql_profile('OUTLINE_TEST_20201222_profile'); -- 删除sql_profile
			2>	sql_profile 使用STA 生成sql_profile；
				
			
ORACLE 相關設置            
	1.  .bash_profile  設置環境變量設置
		PATH=$PATH:$HOME/bin  它的作用是在原来的PATH变量加上家目录下的bin目录的路径，效果就是家目录下的bin目录的命令可以直接打出来执行
		export PATH
		umask 022 權限 755  ( 027-->750)
		unset USERNAME  用于删除已定义的shell变量
		export ORACLE_BASE=/home/oracle
		export ORACLE_SID=tjepd6db
		export ORACLE_HOME=/home/oracle/product/11.2.3/db_1
		export PATH=$ORACLE_HOME/bin:$PATH
		export NLS_LANG=AMERICAN_AMERICA.AL32UTF8
		export PS1=*\${ORACLE_SID}*$PS1 
		stty erase  ^H ctrl+backup 鍵,設置該環境變量只需要backup刪除即可；

	2.  創建快速回復區 (需要切換至mount 狀態下)設置
		1>  設置快速恢復區大小：alter system set db_recovery_file_dest_size = 2G scope=both;
		2>  設置路徑：alter system set db_recovery_file_dest='/u01/app/FAR' scope=both

	3.  oracle 管控ip 訪問之黑白名单 相关（在 文件 sqlnet.ora下設置）
		1>  TCP.VALIDNODE_CHECKING = YES  
			開啟黑白名單訪問按鈕，并使用这个参数来启用下边的两个参数。
		2>  TCP.EXCLUDED_NODES = (list of IP addresses)
			指定不允许访问oracle的节点，可以使用主机名或者IP地址
		3>  TCP.INVITED_NODES = (list of IP addresses)
			指定允许访问db的客户端，他的优先级比TCP.EXCLUDED_NODES高。
			注意：excluded_nodes与invited_nodes为互斥方式，不可以同时使用0
		4>	在設置完成黑白名單后，lsnrctl reload 不會影響現有的鏈接

	4.  登录方式限定 
		在 文件 sqlnet.ora下設置
		SQLNET.AUTHENTICATION_SERVICES= (NTS/NONE/ALL) 
		NTS: 允許ora_dba組中的用戶使用local windows 驗證
		NONE:不允許windows，但允許密碼驗證
		ALL:所有的认证方式都支持

	5.  其它限制
		时间限制
		连接超时时间，即连接300秒没有活动自动断开连接
		sqlnet.expire_time = 300

	6.  版本限制
		可以对客户端的版本进行限制;
		SQLNET_ALLOWED_LOGON_VERSION=8
		SQLNET.ALLOWED_LOGON_VERSION_SERVER=8
		SQLNET.ALLOWED_LOGON_VERSION_CLIENT=8
	7.  內存設置 
		1>  修改SGA 和PGA
			alter system set sga_max_size=83G scope=spfile;
			alter system set sga_target=83G scope=spfile;
			alter system set pga_aggregate_target=1G scope=spfile;
			注：oracle 11g
			若spfile 中只有sga_target 参数，则sga_max_size=sga_target 的值；
			

		2>  修改 memory
			關機Remove the MEMORY_MAX_TARGET=0 and MEMORY_TARGET=0 lines.
			開機alter system reset memory_target;
			alter system reset memory_max_target;
		3>	OS kernel 參數設置
			kernel.shmmax ：是核心参数中最重要的参数之一，用于定义单个共享内存段的最大值。
			kernel.shmall ：该参数控制可以使用的共享内存的总页数。 Linux 共享内存页大小为 4KB, 共享内存段的大小都是共享内存页大小的整数倍。
			下面专门说说kernel.sem：对应4个值
			kernel.sem
			SEMMSL、SEMMNS、SEMOPM、SEMMNI
			SEMMSL: 每个信号集的最大信号数量
			数据库最大 PROCESS 实例参数的设置值再加上 10 。
			Oracle 建议将 SEMMSL 的值设置为不少于 100 。
			SEMMNS：用于控制整个 Linux 系统中信号（而不是信号集）的最大数。
			Oracle 建议将 SEMMNS 设置为：系统中每个数据库的 PROCESSES 实例参数设置值的总和，加上最大 PROCESSES 值的两倍，最后根据系统中 Oracle 数据库的数量，每个加 10 。
			使用以下计算式来确定在 Linux 系统中可以分配的信号的最大数量。它将是以下两者中较小的一个值：SEMMNS 或 (SEMMSL * SEMMNI)

			SEMOPM： 内核参数用于控制每个 semop 系统调用可以执行的信号操作的数量。semop 系统调用（函数）提供了利用一个 semop 系统调用完成多项信号操作的功能。一个信号集能够拥有每个信号集中最大数量的SEMMSL 信号，因此建议设置 SEMOPM 等于SEMMSL 。
			Oracle 建议将 SEMOPM 的值设置为不少于 100 。
			SEMMNI ：内核参数用于控制整个 Linux 系统中信号集的最大数量。
			Oracle 建议将 SEMMNI 的值设置为不少于 100 。

设计完这些之后可以 sysctl -p 生效，剩下可以配置AMM.
	8.  並發session 和processors 的設置（修改processes和sessions值必须重启Oracle服务器才能生效）sessions=(1.1*process+5)
		1>  SQL>show parameter processes;
			SQL>show parameter sessions ;
			SQL>alter system set processes=300 scope=spfile;
			SQL>alter system set sessions=335 scope=spfile ;
			SQL>commit ;
		2>	查看查询数据库当前进程的连接数
			select count(*) from v$process;
		3>	查看数据库当前会话的连接数：
			select count(*) from v$session;
		4>	查看数据库的并发连接数：
			select count(*) from v$session where status='ACTIVE';			
	9.	監聽設置
		lsnrctl set log_status off
		mv listener.log listener.log.10
		lsnrctl set log_status on
	10.	oracle 字符集編碼設置
		1>	UTF-32：統一使用4個字節表示一個字符，存在空間利用率問題；
		2>	UTF-16：相對常用的60000 多個字符使2個字節，其餘使用4個字節；
		3>	UTF-8 ：兼容ASCII 拉丁文，希臘文等使用2個字節，包括漢字在內的其他常用字符使用3個字節，剩下的極少的字符使用4個字節；
		4>	oracle 數據庫服務器字符集，
	11.	oracle nls_date_language 顯示格式設定；
		1>	查询nls_date_format
			1)select * from nls_session_parameters where parameter = 'NLS_DATE_FORMAT';
			显示：NLS_DATE_FORMAT      DD-MON-RR
			2)select * from nls_database_parameters where parameter = 'NLS_DATE_FORMAT';
			显示：NLS_DATE_FORMAT    DD-MON-RR
			3)select * from nls_instance_parameters where parameter = 'NLS_DATE_FORMAT';
			显示：NLS_DATE_FORMAT    null  （在我本地nls_instance_parameters中没有设置NLS_DATE_FORMAT）

		2>	设置nls_date_format
			session级别设定值：
			alter session set nls_date_format = 'yyyy-mm-dd hh24:mi:ss';
			设定之后再查询会发现nls_session_parameters视图中nls_date_format的值已经变了，而nls_instance_parameters、nls_database_parameters视图中的值没有变。
			SESSION级别——如果只是希望自己看到某种格式而不影响其他人看到的结果。
			instance级别设定值：
			alter system set nls_date_format = 'yyyy-mm-dd hh24:mi:ss';此级别的值在oracle9i以后就不允许设定了，所以本地设定不了。
			database级别设定值：
			oracle不允许设定此级别的参数值，也没有提供设定语句。
		3>	然后，我们可以通过以下查询，发现本数据库是不允许instance级别和database级别参数值更改的
			select name, isses_modifiable, issys_modifiable, isinstance_modifiable from v$parameter where name = 'nls_date_format';
			显示：nls_date_format    TRUE    FALSE    FALSE
			发现：isses_modifiable=TRUE、issys_modifiable=FALSE、isinstance_modifiable=FALSE的 
oracle 查看修改DB相關命令
	1.  用戶相關
		1>  創建用戶 create user CAILAMEI  identified by  default tablespace test_data  temporary tablespace temp;
		2>  更改用戶名密碼：alter user system identified by password;
		3>  解鎖用戶 alter user user_name account unlock;
		4>  查看用戶 select USERNAME,ACCOUNT_STATUS from dba_users;
		5>  設置所有用戶密碼不過期 
			SELECT USERNAME ,PROFILE FROM dba_users;
			SELECT * FROM dba_profiles s WHERE s.profile='DEFAULT' AND resource_name='PASSWORD_LIFE_TIME';
			ALTER PROFILE DEFAULT LIMIT PASSWORD_LIFE_TIME UNLIMITED;

		6>  設置單個用戶密碼不過期
			創建 profile                     
			CREATE PROFILE "VPXADMIN_UNLIMIT" LIMIT
			SESSIONS_PER_USER UNLIMITED
			CPU_PER_SESSION UNLIMITED
			CPU_PER_CALL UNLIMITED
			CONNECT_TIME UNLIMITED
			IDLE_TIME UNLIMITED
			LOGICAL_READS_PER_SESSION UNLIMITED
			LOGICAL_READS_PER_CALL UNLIMITED
			COMPOSITE_LIMIT UNLIMITED
			PRIVATE_SGA UNLIMITED
			FAILED_LOGIN_ATTEMPTS 10
			PASSWORD_LIFE_TIME 180
			PASSWORD_REUSE_TIME UNLIMITED
			PASSWORD_REUSE_MAX UNLIMITED
			PASSWORD_LOCK_TIME 1
			PASSWORD_GRACE_TIME 7
			PASSWORD_VERIFY_FUNCTION NULL;
			設置新的密碼profile 密碼不過期
			ALTER profile VPXADMIN_UNLIMIT limit PASSWORD_LIFE_TIME UNLIMITED;
			更換用戶profile 文件為新建的文件
			alter user CAILAMEI  profile VPXADMIN_UNLIMIT;
		7>  解鎖用戶		
			select USERNAME,STATUS from dba_users;
			alter user user_name account unlock
		8>  設置密碼不區分大小寫
			show parameter sec_case
			alter system set sec_case_sensitive_logon=false;
			在更新完密碼大小寫后，需要alter 被lock 的帳密；

	2.  授予權限方面
		1>	查詢oracle 的權限列表
			select * from session_privs;
			select * from dba_sys_privs; 查看用户或者角色系统权限
			
			
		2>  獲取schema 下所有的表或者其他對象，得出授予對象權限的腳本，適用於 給所有對象賦權限
			select 'Grant select  on MES1.'||table_name||' to  TJW_READ ;' from all_tables where owner = upper('MES1');  

		3>  授予當前用戶查詢其他用戶表的權限
			Grant select on MES1.C_SAPWORKORDER_TEMP to TJW_READ;

		4>  撤銷當前用戶查詢其他用戶表的權限
			revoke select on MES1.HR_GTWREPORTBYMONTH from TJW_READ;			
		5>  查詢用戶賦予對象的權限
			set pagesize 600
			select 'grant ' ||PRIVILEGE||' on ' ||OWNER||'.'||TABLE_NAME||' to '||GRANTEE||';'  from dba_tab_privs where grantor='SFIS1';
	3.  oracle 性能相關
		1>  查看運行慢的sql 
			select * from 
			(select sa.SQL_TEXT,sa.EXECUTIONS "执行次数",round(sa.ELAPSED_TIME / 1000000, 2) "总执行时间", 
			round(sa.ELAPSED_TIME / 1000000 / sa.EXECUTIONS, 2) "平均执行时间",sa.COMMAND_TYPE,
			sa.PARSING_USER_ID "用户ID",u.username "用户名",sa.HASH_VALUE 
			from v$sqlarea sa
			left join all_users u  on sa.PARSING_USER_ID = u.user_id
			where sa.EXECUTIONS > 0  order by (sa.ELAPSED_TIME / sa.EXECUTIONS) desc)
			where rownum <= 50;

		2>  查看運行最多的sql 
			select * from 
			(select s.SQL_TEXT,s.EXECUTIONS "执行次数",s.PARSING_USER_ID "用户名",rank() over(order by EXECUTIONS desc) EXEC_RANK
			from v$sql s
			left join all_users u on u.USER_ID = s.PARSING_USER_ID) t
			where exec_rank <= 100;
		
		3>	打印awr 報告
			@?/rdbms/admin/awrrpt.sql

	4.  查看oracle schema 對象相關sql  語句
		1>  DB_LINK (公有和私有)
			<1> 查看DB 所有的DBLINk
				set linesize 300;
				col HOST for a20;
				col USERNAME for a20; 
				col DB_LINK for a15;
				select * from ALL_DB_LINKS;

			<2> 創建全局 DBLINK
				CREATE PUBLIC DATABASE LINK EPD1BIG CONNECT TO sfis1 IDENTIFIED BY sfis1 USING '10.67.51.14:1560/tjepd1big';
				CREATE PUBLIC DATABASE LINK MYSQL5TEST CONNECT TO "easyweb" IDENTIFIED BY "webeasy" USING 'MYSQL5TEST';
				select count(*) from test_cailamei@MYSQL5TEST;
			<3> 刪除DBLINK
				DROP DATABASE LINK [name]; /  DROP PUBLIC DATABASE LINK [name];
			<4> 通過DBLINK 查詢數據
				select * from user3.table@testLink;

		2>  查詢創建對象的sql 腳本
			set long 999999;
			SET LINESIZE 1000 
			SET PAGESIZE 1000
			select dbms_metadata.get_ddl('TABLE','TABLE_NAME','TABLE_OWNER') from dual;  --創建表
			select dbms_metadata.get_ddl('INDEX','INDEX_NAME','INDEX_OWNER') from dual; --創建索引
			select dbms_metadata.get_ddl('VIEW','VIEW_NAME','VIEW_OWNER') from dual; --創建視圖
			select dbms_metadata.get_ddl('PROCEDURE','PROCEDURE_NAME','PROCEDURE_OWNER') fromdual;  --創建存儲過程
			select dbms_metadata.get_ddl('FUNCTION','FUNCTION_NAME','FUNCTION_OWNER') from dual;  --創建函數
			SELECT DBMS_METADATA.GET_DDL('CONSTRAINT','CONSTRAINTNAME','USERNAME') FROM DUAL; --查看创建主键的SQL
			SELECT DBMS_METADATA.GET_DDL('REF_CONSTRAINT','REF_CONSTRAINTNAME','USERNAME') FROM DUAL; --查看创建外键的SQL
			SELECT DBMS_METADATA.GET_DDL('USER','USERNAME') FROM DUAL; --查看用户的SQL
			SELECT DBMS_METADATA.GET_DDL('ROLE','ROLENAME') FROM DUAL;--查看角色的SQL
			SELECT DBMS_METADATA.GET_DDL('TABLESPACE','TABLESPACENAME') FROM DUAL; --查看表空间的SQL
			select dbms_metadata.get_ddl('MATERIALIZED VIEW','MVNAME') FROM DUAL; --获取物化视图SQL
			SELECT dbms_metadata.get_ddl('DB_LINK','DBLINKNAME','USERNAME') stmt FROM dual; --获取远程连接定义SQL--
			select DBMS_METADATA.GET_DDL('TRIGGER','TRIGGERNAME','USERNAME) FROM DUAL;--获取用户下的触发器SQL
			select DBMS_METADATA.GET_DDL('SEQUENCE','SEQUENCENAME') from DUAL; -获取用户下的序列
			select DBMS_METADATA.GET_DDL('PACKAGE','PACKAGENAME','USERNAME') from dual; --获取包的定义
			SELECT DBMS_LOB.SUBSTR@dblinkname(DBMS_METADATA.GET_DDL@dblinkname('TABLE', 'TABLENAME', 'USERNAME')) FROM DUAL@dblinkname  --获取远程数据库对象的定义					
		
		3>  查看并編譯 DB 無效的對象
			<1>	查看無效的對象
				SELECT owner,object_type,object_name,STATUS FROM dba_objects WHERE STATUS='INVALID' ORDER BY owner,object_type,object_name;
			<2> 手動編譯單個無效的對象
				SQL>ALTER PROCEDURE my_procedure COMPILE;
				SQL>ALTER FUNCTION my_function COMPILE;
				SQL>ALTER TRIGGER my_trigger COMPILE;
				SQL>ALTER VIEW my_view COMPILE;
			<3> 編譯schema 下所有的對象
				EXEC DBMS_UTILITY.compile_schema(schema =>'CAILAMEI');--使用这个包将会编译指定schema下的所有procedures, functions, packages, and triggers.
			<4> 執行組裝sql 批量編譯無效的對象
				SELECT 'alter '||object_type||' '||owner||'.'||object_name||' compile;' 
				FROM all_objects 
				WHERE status = 'INVALID'  AND object_type in ('FUNCTION','JAVA SOURCE','JAVA CLASS','PROCEDURE','PACKAGE','VIEW','TRIGGER'); 
				編譯public 同義詞
					
				@$ORACLE_HOME/rdbms/admin/utlrp.sql sys 用戶下執行腳本，批量編譯無效的對象
		
		4>  查看并執行job
			<1> 查看job
				select * from all_jobs
				SELECT * FROM user_jobs
				select * FROM dba_jobs where LOG_USER='SFIS1'
				select * FROM dba_jobs_running
				select JOB, LOG_USER, broken  from dba_jobs;
				set linesize 300;
				查看job詳細信息
				col LOG_USER for a10;
				col priv_user for a10;
				col broken for a10;
				col what for a30;
				col job for a10;
				select job, what, log_user, priv_user,broken from dba_jobs where job=208;
			<2> 手動執行job
				exec DBMS_IJOB.broken(208,true);exec DBMS_JOB.BROKEN(486,TRUE)待驗證；
			<3> 創建job
				DECLARE jobno numeric; 
				BEGIN dbms_job.submit(jobno, 'SFISM4.COPY_FROM_STANDBY_EPD1;', sysdate+(9*60+15)/(24*60), 'sysdate+(10/1440)'); 
				COMMIT;
				END;
				
				DECLARE jobno numeric; 
				BEGIN dbms_job.submit(jobno,'SFIS1.BP_TESTTIME;',to_date('2020-01-07 16:00:00','yyyy-mm-dd hh24:mi:ss'),'NEXT_DAY(TRUNC(SYSDATE),1)+1/24');
				COMMIT;
				END;

			<4> 查看正在執行的job 的sid,spid;
				select b.SID,b.SERIAL#,c.SPID
				from dba_jobs_running a,v$session b,v$process c
				where a.sid = b.sid and b.PADDR = c.ADDR
			<5> 殺掉正在執行的job；
				ALTER SYSTEM KILL SESSION '1721,747';
				
			<6> job執行的時間設定
				描述                                 INTERVAL参数值
				每天午夜12点                      'TRUNC(SYSDATE + 1)'
				每天早上8点30分                  'TRUNC(SYSDATE + 1) + （8*60+30）/(24*60)'
				每星期二中午12点                  'NEXT_DAY(TRUNC(SYSDATE ), 'TUESDAY' ) + 12/24'
				每星期六和日早上6点10分    	   'TRUNC(LEAST(NEXT_DAY(SYSDATE, 'SATURDAY'), NEXT_DAY(SYSDATE, 'SUNDAY'))) + (6×60+10)/(24×60)' sunday 有時候需要寫成數字為1
				每个月第一天的午夜12点          'TRUNC(LAST_DAY(SYSDATE ) + 1)'
				每个季度最后一天的晚上11点     'TRUNC(ADD_MONTHS(SYSDATE + 2/24, 3 ), 'Q' ) -1/24'
		5>  查看某一個對象
			select  * from all_source where text like '%UPDATE SFISM4.R_WIP_TRACKING_T%';
		6>	創建同義詞
			CREATE [OR REPLACE] [PUBLIC] SYNONYM [当前用户.]synonym_nameFOR [其他用户.]object_name;
		7>	查看並創建序列
			create sequence aaa increment by 1 start with 1;
			select aaa.nextval from dual;
			select aaa.currval from dual;	
	5.	表分區
		1>  select * FROM dba_tables where owner='SFISM4' and partitioned='YES'		
	6.  表空間相關
		1>  查看表空間狀態
			select tablespace_name,status from dba_tablespaces;
		2>	表空間收縮 
			select  a.file_id,a.file_name,a.filesize, b.freesize, 
			(a.filesize-b.freesize) usedsize,  c.hwmsize,  
			c.hwmsize - (a.filesize-b.freesize) unsedsize_belowhwm,  
			a.filesize - c.hwmsize canshrinksize  
			from  
			(select file_id,file_name,round(bytes/1024/1024) filesize from dba_data_files ) a, 
			( select file_id,round(sum(dfs.bytes)/1024/1024) freesize from dba_free_space dfs group by file_id ) b, 
			( select file_id,round(max(block_id)*8/1024) HWMsize from dba_extents group by file_id) c 
			where a.file_id = b.file_id  and a.file_id = c.file_id 
			order by unsedsize_belowhwm desc;
			
	7.  數據文件相關
		1>  修改數據文件名稱或位置		
			<1> DB OPEN 狀態下offline 數據文件并重命名數據文件名稱或修改位置
				set linesize 300
				set pagesize 500
				col NAME for a80
				select NAME, status,ENABLED FROM v$datafile;
				alter database datafile '/data/oradata/CAILAMEI/datafile/CAILAMEI01.dbf' offline;
				alter database rename file '/data/oradata/CAILAMEI/datafile/CAILAMEI01.dbf' to '/data/oradata/CAILAMEI/datafile/test/CAILAMEI01test.dbf';
				alter database recover datafile 6;
				alter database datafile '/data/oradata/CAILAMEI/datafile/test/CAILAMEI01test.dbf' online;
				alter tablespace CAILAMEI offline;
			
			<2> DB OPEN 狀態下offline 表空間來修改數據文件名稱或位置的方法
				alter database rename file '/data/oradata/CAILAMEI/datafile/test/CAILAMEI01test.dbf' to '/data/oradata/CAILAMEI/datafile/CAILAMEI01.dbf';
				alter tablespace CAILAMEI online;
			
			<3> DB 停機修改數據文件名稱或位置:
				*  关闭数据库；                   
				*  复制数据文件到新的位置；       
				*  启动数据库到mount状态；        
				*  通过SQL修改数据文件位置；alter database rename file '/opt/oracle/oradata/ZERONE01.DBF' to '/home/oracle/oradata/zerone/ZERONE01.DBF';   
				*  打开数据库；
				*  檢查數據文件：select name from v$datafile;
		
		2>  查看某個表空間下的所有數據文件				
			select file_name,tablespace_name from dba_data_files where tablespace_name='ZERONE';
		3>  查看永久表空间的数据文件对应的表空间
			select TABLESPACE_NAME from dba_data_files where FILE_NAME='数据文件全路径';
		4>  查看临时表空间的数据文件对应的临时表空间
			select TABLESPACE_NAME from dba_temp_files where FILE_NAME='数据文件全路径';
	8.	oracle 常用函數
		1>	常用日期函数
			select to_char(sysdate, 'yyyy') 年,
			to_char(sysdate, 'mm') 月,
			to_char(sysdate, 'DD') 日,
			to_char(sysdate, 'HH24') 时,
			to_char(sysdate, 'MI') 分,
			to_char(sysdate, 'SS') 秒,
			to_char(sysdate, 'DAY') 天,
			to_char(sysdate, 'Q') 第几季度,
			to_char(sysdate, 'W') 当月第几周,
			to_char(sysdate, 'WW') 当年第几周,
			to_char(sysdate, 'D') 当周第几天,
			to_char(sysdate, 'DDD') 当年第几天    
			from dual;			
			select decode(200, 100, 100, '200', '200', '300') from dual;
			select trunc(sysdate) from dual;2020-10-08 00:00:00 -- 截取至年月日，時分秒用0補齊
			select round(to_date('2020-10-08 12:30:59','yyyy-mm-dd hh24:mi:ss')) from dual;2020-10-09 00:00:00 -- syadate 當天超過12點，則結果值為後一天的年月日：00：00:00
			select trunc(sysdate,'MONTH') from dual;--2020-10-01 00:00:00 --截取到月
			select trunc(sysdate,'YEAR') from dual;--截取到年2020-01-01 00:00:00
			select round(sysdate,'MONTH') from dual; 2020-10-01 00:00:00
			select round(sysdate,'YEAR') from dual;2021-01-01 00:00:00
		2>	常用分析函数
			<1>	first_value()和last_value() 取首尾记录值。
				select
				dept_id
				,sale_date
				,goods_type
				,sale_cnt
				,first_value(sale_date) over (partition by dept_id order by sale_date) first_value
				,last_value(sale_date) over (partition by dept_id order by sale_date desc) last_value
				from criss_sales;

				注：last_value()默认统计范围是 rows between unbounded preceding and current row

				select
				dept_id,
				sale_date
				goods_type,
				sale_cnt,
				first_value(sale_date) over (partition by dept_id order by sale_date) first_value, -- 取第一个值
				last_value(sale_date) over (partition by dept_id order by sale_date desc) last_value, --取当前值
				last_value(sale_date) over (partition by dept_id order by sale_date rows between unbounded preceding and unbounded following) last_value_all  -- 取最大值
				from criss_sales;
			<2>	sql 四大排名函数(ROW_NUMBER、RANK、DENSE_RANK、NTILE(不做研究))
				select row_number() over(order by score desc) rn,score,name from clm_test;
				rn		score	name
				------	-------	--------------
				1		90		chensen
				2		85		cailamei
				3		85		yangfei
				4		80		chenyoulin
				5		70		laji
				
				select rank() over(order by score desc) rn,score,name from clm_test;
				rn		score	name
				------	-------	--------------
				1		90		chensen
				2		85		cailamei
				2		85		yangfei
				4		80		chenyoulin
				5		70		laji
				
				select DENSE_RANK() over(order by score desc) rn,score,name from clm_test;
				rn		score	name
				------	-------	--------------
				1		90		chensen
				2		85		cailamei
				2		85		yangfei
				3		80		chenyoulin
				4		70		laji
				
	9.	oracle 參數設置(動態&靜態) 
		select distinct ISSYS_MODIFIABLE from v$parameter；
		IMMEDIATE --動態參數
		FALSE --靜態參數
		DEFERRED --動態參數
		--查詢參數的靜態或動態屬性
		select name,ISSYS_MODIFIABLE from v$parameter  where name='sec_case_sensitive_logon';
	10.	Oracle 查看存储过程占用，及编译时卡住问题；
		1>  查看存储过程是否有锁住 --LOCKS!='0' 即表示有锁，正在执行 --name 这里也可以用like来模糊拆线呢
			SELECT * FROM V$DB_OBJECT_CACHE WHERE name='存储过程名称' AND LOCKS!='0';
		2>  找到锁住过程的SID ---object这里一样可以用 like  模糊
			select  SID from V$ACCESS WHERE object='存储过程名称';
		3>  查看锁住存储过程对象的设备信息，包括是那台机器锁定的,什么时间锁住的，等等都可以通过以下语句查到
			SELECT *  FROM V$SESSION WHERE SID='SID';
		4>  强制kill进程,先找到要杀死进程的sid 和 serial# ,然后进行kill (注意， 这里的alter命令，可以加immediate 也可以不加immediate,加immediate ，表示标记执行，类似异步吧,不加immediate：表示直接立即执行，这个时候有可能出现plsql程序假死的情况。)
			SELECT SID,SERIAL#,PADDR FROM V$SESSION WHERE SID='sid';
			alter system kill session 'SID，Serial#' immediate 
	11.	resize 數據文件
		select a.file#,a.name,a.bytes / 1024 / 1024 CurrentMB,
		ceil(HWM * a.block_size) / 1024 / 1024 ResizeTo,
		(a.bytes - HWM * a.block_size) / 1024 / 1024 ReleaseMB,'alter database datafile ''' || a.name || ''' resize ' ||ceil(ceil(HWM * a.block_size) / 1024 / 1024) || 'M;' ResizeCmd
		from v$datafile a,
		(SELECT file_id, MAX(block_id + blocks - 1) HWM FROM DBA_EXTENTS
		GROUP BY file_id) b
		where a.file# = b.file_id(+)
		And (a.bytes - HWM * a.block_size) >0
		order by ReleaseMB desc;
	12.	刪除數據庫；（數據文件& 控制文件 & 日誌文件）
		SQL> shutdown immediate
			 Database closed.
			 Database dismounted.
			 ORACLE instance shut down.
		SQL> startup nomount;
			 ORACLE instance started.
			 Total System Global Area 1820540928 bytes
			 Fixed Size		    2229304 bytes
			 Variable Size		  855641032 bytes
			 Database Buffers	  956301312 bytes
			 Redo Buffers		    6369280 bytes
		SQL> alter database mount exclusive;
			 Database altered.
		SQL> alter system enable restricted session;
			 System altered.
		SQL> drop database;
			 Database dropped.
			 
	13.	查看oracle 安裝的組件：
		SQL> set pagesize 1000
		SQL> col comp_name format a36
		SQL> col version format a12
		SQL> col status format a8
		SQL> col owner format a12
		SQL> col object_name format a35
		SQL> col name format a25
		select comp_name, version, status from dba_registry;
		
		COMP_NAME			               VERSION	   STATUS
		OWB				              11.2.0.3.0   VALID
		Oracle Application Express	          3.2.1.00.12  VALID
		Oracle Enterprise Manager	          11.2.0.3.0   VALID
		OLAP Catalog			              11.2.0.3.0   VALID
		Spatial 			                  11.2.0.3.0   VALID
		Oracle Multimedia		              11.2.0.3.0   VALID
		Oracle XML Database		          11.2.0.3.0   VALID
		Oracle Text			              11.2.0.3.0   VALID
		Oracle Expression Filter	              11.2.0.3.0   VALID
		Oracle Rules Manager		          11.2.0.3.0   VALID
		Oracle Workspace Manager	          11.2.0.3.0   VALID
		Oracle Database Catalog Views	      11.2.0.3.0   VALID
		Oracle Database Packages and Types    11.2.0.3.0   VALID
		JServer JAVA Virtual Machine	      11.2.0.3.0   VALID
		Oracle XDK			              11.2.0.3.0   VALID
		Oracle Database Java Packages	      11.2.0.3.0   VALID
		OLAP Analytic Workspace 	          11.2.0.3.0   VALID
		Oracle OLAP API 		              11.2.0.3.0   VALID
	14.	更新db_name,instance_name,		
		1>	更新db_unique_name
			alter system set db_unique_name='GDLSFCDB' scope=spfile;
			shutdown immediate;
		2>	更新listener.ora
		3>	更新 .bash_profile
		4>	更新 /etc/oratab
		5>	修改 $ORACLE_HOME/dbs 下所有帶有原先實例的文件改為GDLSFCDB；
		6>	export ORACLE_SID=GDLSFCDB
		7>	DB 啟動到mount 修改 db_name
			oracle 用戶下執行 nid target=sys dbname=GDLSFCDB setname=YES;控制文件中的DB_name 可以直接修改
			執行完成後instance 會shutdown
		8>	DB 啟動到nomount 修改 db_name(不做nid 不能直接修改DB_NAME)
			alter system set db_name=GDLSFCDB scope=spfile;
			create pfile from spfile;
			shutdown immediate
		9>	修改參數文件將DB_NAME 修改為GDLSFCDB 
		10>	修改控制文件路徑；
			startup nomount
			show parameter control;
			alter system set control_files='/data/oradata/GDLSFCDB/controlfile/o1_mf_hbnl5lnc_.ctl' scope=spfile;
			shutdown immediate;
		11>	startup mount 修改數據文件路徑；
			select NAME, status,ENABLED FROM v$datafile;
			select * from v$datafile;
			select 'alter database rename file '''||NAME||''' to ''/data/oradata/GDLSFCDB/datafile/'||substr(name,36) ||''';' FROM v$datafile where file# !=26;

			alter database rename file '/data/oradata/GDLSFC2TEST/datafile/o1_mf_system_hbnl47xb_.dbf' to '/data/oradata/GDLSFCDB/datafile/o1_mf_system_hbnl47xb_.dbf';
			alter database rename file '/data/oradata/GDLSFC2TEST/datafile/o1_mf_sysaux_hbnl47xx_.dbf' to '/data/oradata/GDLSFCDB/datafile/o1_mf_sysaux_hbnl47xx_.dbf';
			alter database rename file '/data/oradata/GDLSFC2TEST/datafile/o1_mf_undotbs1_hbnl47xz_.dbf' to '/data/oradata/GDLSFCDB/datafile/o1_mf_undotbs1_hbnl47xz_.dbf';
			alter database rename file '/data/oradata/GDLSFC2TEST/datafile/o1_mf_users_hbnl47yh_.dbf' to '/data/oradata/GDLSFCDB/datafile/o1_mf_users_hbnl47yh_.dbf';
			alter database rename file '/data/oradata/GDLSFC2TEST/datafile/BASE_DATA01.dbf' to '/data/oradata/GDLSFCDB/datafile/BASE_DATA01.dbf';
			alter database rename file '/data/oradata/GDLSFC2TEST/datafile/BASE_IDX01.dbf' to '/data/oradata/GDLSFCDB/datafile/BASE_IDX01.dbf';
			alter database rename file '/data/oradata/GDLSFC2TEST/datafile/CWMLITE01.dbf' to '/data/oradata/GDLSFCDB/datafile/CWMLITE01.dbf';
			alter database rename file '/data/oradata/GDLSFC2TEST/datafile/DRSYS01.dbf' to '/data/oradata/GDLSFCDB/datafile/DRSYS01.dbf';
			alter database rename file '/data/oradata/GDLSFC2TEST/datafile/ICT_DATA01.dbf' to '/data/oradata/GDLSFCDB/datafile/ICT_DATA01.dbf';
			alter database rename file '/data/oradata/GDLSFC2TEST/datafile/o1_mf_ict_data_hbz9x639_.dbf' to '/data/oradata/GDLSFCDB/datafile/o1_mf_ict_data_hbz9x639_.dbf';
			alter database rename file '/data/oradata/GDLSFC2TEST/datafile/ICT_IDX01.dbf' to '/data/oradata/GDLSFCDB/datafile/ICT_IDX01.dbf';
			alter database rename file '/data/oradata/GDLSFC2TEST/datafile/INDX01.dbf' to '/data/oradata/GDLSFCDB/datafile/INDX01.dbf';
			alter database rename file '/data/oradata/GDLSFC2TEST/datafile/LOG_DATA01.dbf' to '/data/oradata/GDLSFCDB/datafile/LOG_DATA01.dbf';
			alter database rename file '/data/oradata/GDLSFC2TEST/datafile/LOG_IDX09.dbf' to '/data/oradata/GDLSFCDB/datafile/LOG_IDX09.dbf';
			alter database rename file '/data/oradata/GDLSFC2TEST/datafile/REC_DATA01.dbf' to '/data/oradata/GDLSFCDB/datafile/REC_DATA01.dbf';
			alter database rename file '/data/oradata/GDLSFC2TEST/datafile/REC_IDX01.dbf' to '/data/oradata/GDLSFCDB/datafile/REC_IDX01.dbf';
			alter database rename file '/data/oradata/GDLSFC2TEST/datafile/SN_DATA01.dbf' to '/data/oradata/GDLSFCDB/datafile/SN_DATA01.dbf';
			alter database rename file '/data/oradata/GDLSFC2TEST/datafile/SN_IDX01.dbf' to '/data/oradata/GDLSFCDB/datafile/SN_IDX01.dbf';
			alter database rename file '/data/oradata/GDLSFC2TEST/datafile/SPI_001.dbf' to '/data/oradata/GDLSFCDB/datafile/SPI_001.dbf';
			alter database rename file '/data/oradata/GDLSFC2TEST/datafile/TRACK_DATA01.dbf' to '/data/oradata/GDLSFCDB/datafile/TRACK_DATA01.dbf';
			alter database rename file '/data/oradata/GDLSFC2TEST/datafile/TRACK_IDX01.dbf' to '/data/oradata/GDLSFCDB/datafile/TRACK_IDX01.dbf';
			alter database rename file '/data/oradata/GDLSFC2TEST/datafile/WIP_DATA01.dbf' to '/data/oradata/GDLSFCDB/datafile/WIP_DATA01.dbf';
			alter database rename file '/data/oradata/GDLSFC2TEST/datafile/WIP_IDX01.dbf' to '/data/oradata/GDLSFCDB/datafile/WIP_IDX01.dbf';
			alter database rename file '/data/oradata/GDLSFC2TEST/datafile/LOG_IDX01.dbf' to '/data/oradata/GDLSFCDB/datafile/LOG_IDX01.dbf';
			alter database rename file '/data/oradata/GDLSFC2TEST/datafile/SN_IDX02.dbf' to '/data/oradata/GDLSFCDB/datafile/SN_IDX02.dbf';

			select 'alter database rename file '''||NAME||''' to ''/data/oradata/GDLSFCDB/datafile/'||substr(name,43) ||''';' FROM v$datafile where file#=26;
			alter database rename file '/home/oracle/oradata/GDLSFC2TEST/datafile/SN_IDX03.dbf' to '/data/oradata/GDLSFCDB/datafile/SN_IDX03.dbf';
		12>	修改臨時文件路徑；
			select * from dba_temp_files;
			select 'alter database rename file '''||file_name||''' to ''/data/oradata/GDLSFCDB/datafile/'||substr(file_name,36) ||''';' FROM dba_temp_files;
			alter database rename file '/data/oradata/GDLSFC2TEST/datafile/o1_mf_temp_hbnl5pmb_.tmp' to '/data/oradata/GDLSFCDB/datafile/o1_mf_temp_hbnl5pmb_.tmp';
		13>	修改online日誌路徑：
			select * from v$logfile;
			select 'alter database rename file '''||MEMBER||''' to ''/data/oradata/GDLSFCDB/onlinelog/'||substr(MEMBER,37) ||''';' FROM v$logfile;

			alter database rename file '/data/oradata/GDLSFC2TEST/onlinelog/o1_mf_3_hbnl5ok1_.log' to '/data/oradata/GDLSFCDB/onlinelog/o1_mf_3_hbnl5ok1_.log';
			alter database rename file '/data/oradata/GDLSFC2TEST/onlinel   og/o1_mf_2_hbnl5o85_.log' to '/data/oradata/GDLSFCDB/onlinelog/o1_mf_2_hbnl5o85_.log';
			alter database rename file '/data/oradata/GDLSFC2TEST/onlinelog/o1_mf_1_hbnl5nrt_.log' to '/data/oradata/GDLSFCDB/onlinelog/o1_mf_1_hbnl5nrt_.log';
			alter database rename file '/data/oradata/GDLSFC2TEST/onlinelog/redo01.log' to '/data/oradata/GDLSFCDB/onlinelog/redo01.log';
			alter database rename file '/data/oradata/GDLSFC2TEST/onlinelog/redo02.log' to '/data/oradata/GDLSFCDB/onlinelog/redo02.log';
			alter database rename file '/data/oradata/GDLSFC2TEST/onlinelog/redo03.log' to '/data/oradata/GDLSFCDB/onlinelog/redo03.log';
	15>	降低水位線			  
			analyze table test compute statistics; alter table table_name move; alter table table_name shrink space(compact/cascade);	  
			使用move时，会改变一些记录的ROWID，所以MOVE之后索引会变为无效，需要REBUILD。
			alter table test move storage (initial 1m) 可以壓縮高水位線
			所以MOVE并不算真正意义上的压缩空间，只会压缩HWM以下的空间，消除碎片。我们一般建表时没有指定initial参数(默认是8个BLOCK)，也就感觉不到这个差异。而SHRINK SPACE真正做到了对段的压缩，包括初始分配的也压了，所以它是blow and above HWM操作
			使用shrink space时，索引会自动维护。如果在业务繁忙时做压缩，可以先shrink space compact，来压缩数据而不移动HWM，等到不繁忙的时候再shrink space来移动HWM。
			索引也是可以压缩的，压缩表时指定Shrink space cascade会同时压缩索引，也可以alter index xxx shrink space来压缩索引。
			shrink space需要在表空间是自动段空间管理的，所以system表空间上的表无法shrink space。
			
			select segment_name,header_block,blocks from dba_segments where segment_name in ('TEST1','TEST2');
			TEST1	194	    51080
			TEST2	55498	52224
			select TABLE_NAME,BLOCKS,EMPTY_BLOCKS from dba_tables where table_name  in ('TEST1','TEST2');
			TEST2	51277	947 block 即是高水位線
			TEST1	50769	311
			analyze table cailamei.test1 compute statistics;
			select count(distinct dbms_rowid.rowid_block_number(rowid)) used_blocks from cailamei.test1;
			50769
	16>	truncate/drop/delete 區別以及表空間的釋放
			truncate drop  delete 都不釋放磁盤空間；
			truncate drop purge 表空間資源釋放；
			drop 不加purge 表空間釋放，但是會在回收站；
			delete 不會降低水位；
	17>	更改數據庫字符集
		查看DB編碼
		select * from nls_database_parameters where parameter ='NLS_CHARACTERSET';--查詢數據庫字符集
		select * from nls_instance_parameters where parameter='NLS_LANGUAGE';-- 查詢client 端字符集
		select sys_context('userenv','language') from dual; 查詢client 端字符集

		oracle 数据库 NLS_CHARACTERSET 字符集的修改
		先连接数据库：打开命令窗口输入： sqlplus / as sysdba
		步骤：
		SQL> SHUTDOWN IMMEDIATE;
		SQL> STARTUP MOUNT;
		SQL> ALTER SESSION SET SQL_TRACE=TRUE;
		SQL> ALTER SYSTEM ENABLE RESTRICTED SESSION;
		SQL> ALTER SYSTEM SET JOB_QUEUE_PROCESSES=0;
		SQL> ALTER SYSTEM SET AQ_TM_PROCESSES=0;
		SQL> ALTER DATABASE OPEN;
		SQL> set linesize 120;
		SQL> ALTER DATABASE CHARACTER SET ZHS16GBK;ZHT16BIG5
		执行过程中常见问题：
		问题1:
		SQL> ALTER DATABASE CHARACTER SET ZHS16CGB231280;
		ALTER DATABASE CHARACTER SET ZHS16CGB231280
		ERROR at line 1:
		ORA-12712: new character set must be a superset of old character set
		原因:
		字符集超集问题，所谓超集是指:当前字符集中的每一个字符在新字符集中都可以表示，并使用同样的代码点，比如很多字符集都是US7ASCII的严格超集。如果不是超集，将获得以上错误。
		解决方式:
		SQL> alter database character set internal_use ZHS16GBK;
		SQL> select * from v$nls_parameters;
		SQL> SHUTDOWN IMMEDIATE;
		SQL> STARTUP;
		备注:
		ALTER DATABASE CHARACTER SET操作的内部过程是完全相同的，也就是说INTERNAL_USE提供的帮助就是使Oracle数据库绕过了子集与超集的校验。该方法某些方面有用处，比如测试环境;应用于产品环境大家应该格外小心，除了你以外，没有人会为此带来的后果负责。
		问题2:
		ALTER DATABASE CHARACTER SET ZHS16GBK
		ERROR at line 1:
		ORA-12721: operation cannot execute when other sessions are active
		原因:
		字符集超集问题。
		解决方式:
		SQL> alter database character set internal_use ZHS16GBK;
		SQL> select * from v$nls_parameters;
		SQL> SHUTDOWN IMMEDIATE;
		SQL> STARTUP;
		问题3:
		SQL> ALTER DATABASE CHARACTER SET ZHS16GBK;
		ALTER DATABASE CHARACTER SET ZHS16GBK
		*
		ERROR at line 1:
		ORA-12716: Cannot ALTER DATABASE CHARACTER SET when CLOB data exists
		原因:
		数据库存在CLOB类型字段，那么就不允许对字符集进行转换
		解决方式:
		这时候，我们可以去查看alert.log日志文件，看CLOB字段存在于哪些表上:
		内容如：
		ALTER DATABASE CHARACTER SET ZHS16GBK
		SYS.METASTYLESHEET (STYLESHEET) - CLOB populated
		ORA-12716 signalled during: ALTER DATABASE CHARACTER SET ZHS16GBK...
		对于用户表，可以先将该表导出，然后把该表删掉，等字符转换完毕后在导入	
	18> 普通表在線轉分區表及set unused column 的恢復；
		EXEC DBMS_REDEFINITION.CAN_REDEF_TABLE('SFISM4','R_TRACK_TIME_T',dbms_redefinition.cons_use_rowid);--基於rowid
		EXEC DBMS_REDEFINITION.CAN_REDEF_TABLE('SFISM4','R_TRACK_TIME_T',DBMS_REDEFINITION.CONS_USE_PK);--基於主鍵（默認）
		exec dbms_redefinition.start_redef_table('SFISM4','R_TRACK_TIME_T','P_R_TRACK_TIME_T',null,dbms_redefinition.cons_use_rowid); 
		--如果start成功不需要执行abort
		exec dbms_redefinition.abort_redef_table('SFISM4','R_TRACK_TIME_T','P_R_TRACK_TIME_T');
		創建index/授權等
		exec dbms_redefinition.sync_interim_table('SFISM4','R_TRACK_TIME_T','P_R_TRACK_TIME_T');
		exec dbms_redefinition.finish_redef_table('SFISM4','R_TRACK_TIME_T','P_R_TRACK_TIME_T');
		Drop table SFISM4.P_R_TRACK_TIME_T purge;
		select * from dba_tab_cols where table_name= ‘R_TRACK_TIME_T’
		ALTER TABLE SFISM4.P_R_TRACK_TIME_T SET UNUSED (xxx);
		alter table  "SFISM4"."R_TRACK_TIME_T" drop unused columns;
		设置UNUSED 列之后，并不是将该列数据立即删除，而是被隐藏起来，物理上还是存在的，因此可以恢复，但是恢复过程需要修改底层的数据字典并重启数据库，因此在执行SET UNUSED操作时务必慎重！
		恢復語法如下：
		SELECT OBJECT_ID FROM USER_OBJECTS where OBJECT_NAME= 'HOEGH';
		select cols from tab$ where obj#=102893;
		update col$ set col#=intcol# where obj#=102893;
		update tab$ set cols=cols+1 where obj#=102893;
		update col$ set name='DYNASTY' where obj#=102893 and col#=3;
		update col$ set property=0 where obj#=102893;
	19>	分區表
		PARTITION BY RANGE ("CREATE_DATE") INTERVAL (NUMTOYMINTERVAL(1,'MONTH')) /INTERVAL (NUMTODSINTERVAL(1,'day'))
		(PARTITION "P01"  VALUES LESS THAN (TO_DATE(' 2019-01-01 00:00:00', 'SYYYY-MM-DD HH24:MI:SS', 'NLS_CALENDAR=GREGORIAN')))TABLESPACE "SN_DATA" ;

	20>	修改字段的順序
		SQL> SELECT OBJ#,COL#,NAME FROM SYS.COL$ WHERE OBJ# =74344;    
		OBJ#       COL#        NAME
		---------- ---------- -----------
		74344          1       USERNAME   
		74344          2       USER_ID
		UPDATE SYS.COL$ SET COL# = 1,NAME='ID' WHERE OBJ# = 74344 AND NAME='USER_ID';
		UPDATE SYS.COL$ SET COL# = 2,NAME='NAME' WHERE OBJ# = 74344 AND NAME ='USERNAME';
		重启数据库服务（由于数据字典是在数据库启动时加载到SQL中的，所以修改了它之后，还需要重启数据库服务。）
	21>	修改undo 表空間；
		检查数据库UNDO表空间占用空间情况以及数据文件存放位置；  
		select file_name,bytes/1024/1024 from dba_data_files where tablespace_name like 'UNDOTBS%';
		查看回滚段的使用情况，哪个用户正在使用回滚段的资源，如果有用户最好更换时间（特别是生产环境）。  
		select s.username, u.name from v$transaction t,v$rollstat r, v$rollname u,v$session s   where s.taddr=t.addr and t.xidusn=r.usn and r.usn=u.usn order by s.username;
		检查UNDO Segment状态；  
		SQL> select usn,xacts,rssize/1024/1024/1024,hwmsize/1024/1024/1024,shrinks  
		from v$rollstat order by rssize;  
		USN  XACTS  RSSIZE/1024/1024/1024  HWMSIZE/1024/1024/1024  SHRINKS  
		1    0    0     0.000358582             0.000358582               0  
		2    14   0     0.796791077             0.796791077               735  
		3    44   1     0.00920867919921875     3.99295806884766          996
		这还原表空间中还存在3个回滚的对象。  
		创建新的UNDO表空间，并设置自动扩展参数；  
		SQL> create undo tablespace undotbs2 datafile '/opt/oracle/oradata/ge01/UNDOTBS2.dbf' size 100m reuse autoextend on next 50m maxsize 5000m;   
		8. 切换UNDO表空间为新的UNDO表空间 ， 动态更改spfile配置文件；  
		SQL> alter system set undo_tablespace=undotbs2 scope=both;    
		9.验证当前数据库的 UNDO表空间   
		SQL> show parameter undo  
		NAME                                 TYPE        VALUE  
		------------------------------------ ----------- --------------  
		undo_management                      string      AUTO  
		undo_retention                       integer     900  
		undo_tablespace                      string      UNDOTBS2
		9. 等待原UNDO表空间所有UNDO SEGMENT OFFLINE；  
		select usn,xacts,status,rssize/1024/1024,hwmsize/1024/1024, shrinks from v$rollstat order by rssize;
		select t.segment_name,t.tablespace_name,t.segment_id,t.status from dba_rollback_segs t;  
		SEGMENT_NAME      TABLESPACE_NAME SEGMENT_ID   STATUS  
		1     SYSTEM             SYSTEM          0           ONLINE  
		2     _SYSSMU1$          UNDOTBS1        1           OFFLINE  
		3     _SYSSMU2$          UNDOTBS1        2           OFFLINE  
		4     _SYSSMU47$         UNDOTBS1        47          OFFLINE
		上面对应的UNDOTBS1还原表空间所对应的回滚段均为OFFLINE  
		10.到$ORACLE_HOME/dbs/init$ORACLE_SID.ora如下内容是否发生变更：  
		#cat $ORACLE_HOME/dbs/initddptest.ora  
		……  
		*.undo_management=’AUTO’  
		*.undo_retention=10800  
		*.undo_tablespace=’UNDOTBS2’  
		如果没有发生变更请执行如下语句：  
		SQL> create pfile from spfile;  
		File created.
		11. 删除原有的UNDO表空间；  
		SQL> drop tablespace undotbs1 including contents;
		最后需要在重启数据库或者重启计算机后到存储数据文件的路径下删除数据文件（为什么要手动删除呢：以上步骤只是删除了ORACLE中undo表空间的逻辑关系，即删除了数据文件在数据字典中的关联，不会自动删除项关联的数据文件）。  
		drop tablespace undotbs1 including contents and datafiles;
	22>	查看oracle版本：
		select * from v$version;
		select version from v$instance;
		select version FROM Product_component_version   Where SUBSTR(PRODUCT,1,6)='Oracle';
oracle DG 搭建相關命令
	1.  查看數據庫角色以及狀態
		select database_role,switchover_status from v$database;          
	2.  備庫查看歸檔日誌序號。
	3.  已經應用成功的日誌
		set pagesize 100
		col name for a58
		col applied for a10
		select sequence#,name,applied from v$archived_log order by sequence#;

	4.  正在應用中的日誌
		col name for a58
		col applied for a10
		select sequence#,name,applied from v$archived_log where applied='IN-MEMORY' ;

	5.  沒有應用的日誌
		col name for a58;
		col applied for a10;
		select sequence#,name,applied from v$archived_log where applied='NO' ;
	6.  查看自動同步文件的方式
		show parameter standby
		ALTER SYSTEM SET standby_file_management='AUTO'  SCOPE=BOTH;
	7.	修改pfile 參數值(靜態參數+動態參數)
		alter system set db_unique_name='cailamei_pr'  scope=spfile;
		alter system set fal_client='agile9_PRI' scope=both;
		alter system set fal_server='agile9_STY' scope=both;
		alter system set log_archive_config='dg_config=(agile9,agile9_st)' scope=both;
		alter system set log_archive_dest_2='SERVICE=agile9_STY LGWR ASYNC VALID_FOR=(ONLINE_LOGFILES,PRIMARY_ROLE) DB_UNIQUE_NAME=agile9_st' scope=both;
		alter system set log_archive_dest_state_1=enable;
		alter system set log_archive_dest_state_2=enable;
		alter system set standby_file_management='AUTO';
		alter database force logging;
		alter system set db_file_name_convert='/data/oradata/agile9/','/data/oradata/agile9/' scope=spfile;
		alter system set log_file_name_convert='/data/oradata/agile9/','/data/oradata/agile9/' scope=spfile;
	8.	備援server 應用日誌相關
		開啟應用重做：alter database recover managed standby database disconnect from session;(無standby redo log)
		實時應用重做：alter database recover managed standby database using current logfile disconnect;(有standby redo log)
		關閉應用重做：alter database recover managed standby database cancel;
oracle DG switchover 和failover
	1.  Switchover 角色切換
		備援：
		--col name for a58;
		--col applied for a10;
		--select sequence#,name,applied from v$archived_log where applied='NO' ;
		主庫：
		--alter system switch logfile
		--archive log list
		--select database_role,switchover_status from v$database;
		--alter database commit to switchover to physical standby with session shutdown;
		--select database_role,switchover_status from v$database;
		--shutdown immediate
		備援：
		--alter database recover managed standby database cancel;
		--select database_role,switchover_status from v$database;
		--alter database commit to switchover to primary with session shutdown;
		--alter database open 
		--select database_role,switchover_status from v$database;
		--alter system switch logfile
		service network restart
		主庫：
		--startup mount
		--alter database open read only;
		--alter database recover managed standby database disconnect from session;(無standby redo log)
		--alter database recover managed standby database using current logfile disconnect;(有standby redo log)
		--select sequence#,name,applied from v$archived_log where applied='IN-MEMORY';
		
	2.  Failover 故障切換
		備援：
		alter database recover managed standby database cancel;
		alter database recover managed standby database finish force;
		select database_role from v$database;
		alter database commit to switchover to primary;
oracle snapshot standby 搭建
	1.	查看閃回是否開啟
		select open_mode,log_mode,flashback_on from v$database;
	2.	查看閃回文件大小及存放路徑
		show parameter recovery
		NAME				          TYPE	      VALUE
		------------------------- -----------   ----------- 
		db_recovery_file_dest		     string
		db_recovery_file_dest_size	     big integer     0
		recovery_parallelism		     integer	    0
	3.	設置	db_recovery_file_dest及db_recovery_file_dest_size
		alter system set db_recovery_file_dest='/data/flashback' scope=spfile;
		alter system set db_recovery_file_dest_size=2G scope=spfile;
	4.	重啟DB至mount 狀態
		shutdown immediate
		startup mount
	5.	開啟閃回
		alter database flashback on;   ---在mount 情況下開啟閃回，且必須在歸檔模式下
	6.	開啟DB 并查看閃回是否開啟
		alter database open;
		select open_mode ,log_mode,flashback_on from v$database;
		OPEN_MODE	  LOG_MODE    FLASHBACK_ON
		------------ ------------  ---------------
		READ ONLY	  ARCHIVELOG   YES
	7.	切換DB 為 napshot standby 并查看 切換后的DB 狀態
		alter database convert to snapshot standby;
		select open_mode ,log_mode,flashback_on,database_role from v$database;
		
		OPEN_MODE	   LOG_MODE	FLASHBACK_ON     database_role
		------------ ------------  ---------------   ----------------
		MOUNTED 	   ARCHIVELOG   YES             SNAPSHOT STANDBY
	8.	查看閃回文件的大小和時間點
		select name,storage_size from v$restore_point;
		NAME                                                     STORAGE_SIZE
		----------------------------------------------------    ---------------
		SNAPSHOT_STANDBY_REQUIRED_09/29/2019 16:41:33             52428800

	9.	開啟DB
		alter database open;
oracle snapshot standby 切換為physical standby 
	shutdown immediate;
	startup mount
	alter database convert to physical standby;
	shutdown immediate
	startup
	select open_mode,database_role from v$database;

	OPEN_MODE	   DATABASE_ROLE
	-------------- ----------------
	READ ONLY	   PHYSICAL STANDBY

	alter database recover managed standby database using current logfile disconnect;
SQL_TRACE
	1.	确定trace文件的路径及trace 文件名稱
		1>	SQL> show parameter user_dump_dest，查詢出trace 的路徑
			NAME             TYPE      VALUE
			-------------   -------- ------------------------------------------
			user_dump_dest    string   /home/oracle/diag/rdbms/cailamei/CAILAMEI/trace
	
		2>	查詢當前session 的tracefile;
			SQL> select tracefile from v$process where addr=(select paddr from v$session where sid=(select distinct sid from v$mystat));	
			TRACE_FILE
			-------------------------------------------------------------
			/home/oracle/diag/rdbms/cailamei/CAILAMEI/trace/CAILAMEI_ora_1667.trc

		3>	可以手工更改产生trace文件的名称
			SQL> alter session set tracefile_identifier='mytrace'
			結果是 /home/oracle/diag/rdbms/cailamei/CAILAMEI/trace/CAILAMEI_ora_1667_mytrace.trc		
	2.  启用sql_trace
		1>	启用实例级别的trace
			SQL> alter system set sql_trace=true
			SQL> show parameter sql_trace

			NAME        TYPE             VALUE
			--------- ----------- -------------
			sql_trace   boolean           TURE


		2>	从session级别启动有两种方式，第一跟踪当前的session，第二跟踪其它的session，我们只需要确定备跟踪会话的sid和serial#的值就可以了
			当前session:

			SQL> alter session set sql_trace=ture;

			其它session:
			
			SQL> select sid,serial# from v$session where sid=138;
			SID          SERIAL#
			---------- ----------
			138            2397
			然后执行下面的包来跟踪这个session
			開始：SQL> execute dbms_system.set_sql_trace_in_session(138,2397,true)
			停止：SQL> execute dbms_system.set_sql_trace_in_session(138,2397,false)
	3.	TKPROF工具
		默认生成的trace文件的可读性是比较差的，我们通常会用TKPROF这个工具来格式化在个trace文件。
        語法： tkprof RBKSAFARI_ora_1302.trc new.txt
10046 TRACE事件
	1.	4個trace 級別
		Level 1 等同于sql_trace的功能。
		Level 4 在Level 1的基础上收集绑定变量的信息
		level 8 在Level 1的基础上增加了等等事件的信息
		level 12 等同于Level 4 + Level *，集同时收集绑定变量和等待事件信息
		可以看出level级别越高，收集的信息越全面，我们用下面例子来分别看下这几个级别的作用
	2.	session 級別trace
		SQL> alter session set events '10046 trace name context forever,level 4';
		SQL> alter session set events '10046 trace name context off';
	3.	跟蹤其他session
		exec dbms_monitor.session_trace_enable(session_id=>493,serial_num=>19945,waits=>true,binds=>true);
		exec dbms_monitor.session_trace_disable(session_id=>493,serial_num=>19945);

errorstack & oradebug 跟蹤報錯
	1.	errorstack 使用：
		1>	session 級別：
			alter session set events='1438 trace name errorstack forever,level 3';
			alter session set events='1438 trace name errorstack off';
		2>	實例級別
			alter system set events='1438 trace name errorstack forever,level 3';
			alter system set events='1438 trace name errorstack off';
	2.	oradebug 使用
		1>	oradebug 当前会话		
			SQL> connect / as sysdba
		　　SQL> oradebug setmypid
		　　SQL> oradebug unlimit
		　　SQL> oradebug event 10053 trace name context forever, level 1
		　　SQL> select * from dual;
		　　SQL> oradebug event 10053 trace name context off
		　　SQL> oradebug tracefile_name
		　　	 /chia/web/admin/PTAV3/udump/ptav3_ora_15365.trc
		2>	oradebug 其他会话
			SQL> oradebug setospid 27028
			SQL> oradebug dump processstate 10
			SQL> oradebug TRACEFILE_NAME
                 /u01/app/oracle/admin/dave2/udump/dave2_ora_27028.trc

 dstat 1
 
 通过案例学调优之--10046事件

table、segment、extent、block之间的关系
	table、segment、extent、block是Oracle存储的逻辑结构,BLOCK是Oracle存储的最基本单位，由DB_BLOCK_SIZE指定，通常为8KB，也可以定义为2KB,4KB,16KB,32KB,64KB等，磁盘最小存储单位是sector(512BYTE)
	Oracle数据块由连续的sector组成Oracle读写单位是数据块，应尽量设置BLOCK大小为磁盘数据块大小的整数倍，避免IO浪费
	连续的数据块组成一个分区extent，便于空间管理，包括空间的分配和释放。
	段的空间是以分区为单位分配的，提高了分配空间的效率，但是带来了空间碎片。
	Oracle每个表或索引都会对应著一个段。如果使用分区表或者分区索引，每个分区（partition）都对应着一个段。每个段都有名字，即对象（表、索引）的名字，段由extent组成，但不要求连续。
	一个table至少是一个segment，如果分区表，则每个分区是一个segment，table可以看成是一个逻辑上的概 念，segment可以看成是这个逻辑概念的物理实现；segment由一个或多个extents组成，segment不可以跨表空间但可以跨数据文件；extent由多个连续的blocks组成，不可以跨数据文件；block由1-多个os块组成，是oracle i/o的最小存储单位

sql 語言類型
	数据操纵语言DML：select、insert、update、delete、merge
	数据定义语言DDL：create、alter、drop、truncate、rename
	事务控制语言TCL：commit、rollback、savepoint
	数据控制语言DCL：grant、revoke
oracle 數據類型
	utf-8  一個字符佔用一個byte,漢字佔用3個byte
	char：固定字符，最长2000个
	varchar2：可变长，最长4000最小1
	number：长度范围1~38，可以存整数或浮点数，注意number(m,n)[m为精度，n为小数位数，所以整数为m-n位
	int、numeric、integer、DECIMAL 类型在创建后均转为number类型，int相当于number(22),存储总长度为22的整数

oracle decode函数和case声明
	1.	decode：
		select decode(expr, 'search1', 'result1', 'search2', 'result2', ..., default) from table_xxx;
		select decode('animal', 'cat', '猫', 'dog', '狗', 'chick', '鸡', '其他动物') from table_animal;
		如果没有default，默认default为null。
		decode里有长度限制，最大为255。（oracle很多函数的参数都有长度限制，比如regexp_like正则表达式长度<512byte）
	2.	case：
		case when animal='cat' then '猫'
		when animal='dog' then '狗'
		when animal='chick' then '鸡'
		else '其他动物'
		end 'animal';
		
	3.	区别：
	   1>	case是statement声明，decode是函数
	   2>	case的逻辑操作不仅仅只是等于判断，而decode只能做等值判断
	   3>	case可以做逻辑比较，如<、>、between、like等等
	   4>	case可以跟谓词如in，case when salary in (9000, 10000) then '9K-10K'
	   5>	case可以跟子查询如，case when emp_no in (select mgr_no from dept) then 'dept_mgr'
	   6>	case可以用于PL/SQL construct，而decode只能作为一个函数应用在sql内部
	   7>	case可以作为function/procedure参数使用，decode不能
			exec myproc(case: A when 'three' then 3 else 0 end);  --right
			exec myproc(decode(:a, 'three', 3, 0));  --error
	   8>	case对类型敏感，类型不同会报错，而decode有类型转换
			select decode(200, 100, 100, '200', '200', '300') from dual;
			select case 200 when 100 then 100
			when '200' then '200'
			else '300'
			end test
			from dual;
	   9>	两者对null处理输出不同
			select decode(null, null, 'this is null', 'this is not null') from dual;
			select case null when null then 'this is null'
			else 'this is not null'
			end test
			from dual;
	   10>	最重要一点：case执行速度比decode更优秀
find ./ -mtime +5 |xargs rm -rf
cat /etc/filebeat/filebeat.yml | egrep -v '#|^$'
ssh -R 1880:9.190.26.244:80 racv-l402 --把9.190.26.244:80 转发到racv-l402 的1880 端口
:%s/old_pattern/new_pattern/g
ssh -R anyport:yumserver:80 dest-host             转发yum repo地址


declare
my_sqltext clob;
my_sqltext :='select /*+no_index(OUTLINE_TEST_20201222 OUTLINE_TEST_IDX)*/ * from OUTLINE_TEST_20201222 where OBJECT_NAME='OUTLINE_TEST_20201222'';
my_task_name :=dbms_sqltune.create_tuning_task(
sql_text=>my_sqltext,
user_name=>'CAILAMEI',
scope=>'COMPREHENSIVE',
time_limit=>


)

10.62.170.196  10.62.170.197
10.67.51.164 yum 80
在10.62.170.196 上 执行 ssh -R 1880:10.67.51.164:80 10.62.170.197 即可让197 访问yum 源；