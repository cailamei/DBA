sql server t-sql 学习笔记

1.	SET ANSI_NULLS ON 和 SET QUOTED_IDENTIFIER ON
		USE [Test]
		GO
		SET ANSI_NULLS ON
		GO
		SET QUOTED_IDENTIFIER ON
		GO

	作用和详解：
	USE:指明整个存储过程所调用/使用的数据库，其中Test是我本地建立的数据库名称，USE [Test]就是告诉程序，要调用/使用的是我本地的Test数据库的意思。必须要指明调用/使用的具体数据库。
	GO:该语句不是SQL的语句，表示一个事务结束的标识，告诉程序在go语句之前的所有语句已经确认并提交了，可以进行批处理操作了。当程序运行到go语句时，就会直接对go语句之前的代码进行批处理操作了。
	SET ANSI_NULLS ON:
		表示对空值(null)进行等于(=)或不等于(<>)判断时，遵从 SQL-92 规则(SQL-92规则中，在对空值(null)进行等于(=)或不等于(<>)比较时，取值为false。)	
		<1>	即使是表中字段column_name中包含空值(null),在进行条件判断 where column_name = NULL 时，该select查询语句返回的数据是空的/返回零行。
		<2>	即使是表中字段column_name中包含非空值，在进行条件判断 where column_name <> NULL时，该select查询语句返回的数据是空的/返回零行。
		eg:
		对空值(null)进行等于(=)判断
		select  *  from a where name = null --返回0行
		对包含非空值(<>)判断
		select  *  from  a  where name <> null ----返回0行
	SET ANSI_NULLS OFF:
		表示在对空值(null)进行等于(=)或不等于(<>)比较时，不再遵从SQL-92的规则：
		<1>.当column_name字段中包含了空值(null)，在进行条件判断 where column_name = null 时，该select查询语句会返回表中column_name 字段值为空(null)的数据行。
		<2>.当column_name字段中包含了非空值，在进行条件判断 where column_name <> null 时，该select 查询语句会返回表中column_name 字段值不为空的数据行。
		eg:
		对空值进行等于(=)判断
		select  *  from  a  where name = null  --返回有null 值得行
		对非空进行判断：
		select  *  from a where name <> null  --返回无null 值得行
	SET QUOTED_IDENTIFIER ON:
		表示使用  引用标识符，标识符可以用双引号分隔，但是，文字必须用单引号分隔。
		select "name","age","sex","grade" from a where name = '张三'
		或
		select name,age,sex,grade from a where name = '张三'		
		说明：当设置为ON时，标识符(数据表字段 name)等字段可以用双引号分隔，也可以不用双引号分隔，但是文字部分必须用单引号来分隔，否则会报错。

	SET QUOTED_IDENTIFIER OFF:
		表示标识符不能用双引号分隔，否则标识符会被当做字符串值来返回，不再是字符来返回。而且，文字部分必须用单引号或双引号分隔。
		标识符被双引号分隔情况：
		select "name","age","sex","grade" from a where name = '张三' or name = "a"
		标识符不使用引用标识符的情况：
		select name,age,sex,grade from a where name = '张三' or name = "a"
		说明：当设置为OFF时，标识符是不能用双引号来分隔的，否则标识符就会被当做是字符串来返回，不再是字符来返回了。而且，文字部分是必须要引号来分隔，可以是单引号('')，也可以是双引号("")。
