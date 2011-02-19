//
// ExtSupport.PHP5 - substitute for php5ts.dll
//
// php_open_temporary_file.h
// - this is slightly modified php_open_temporary_file.h, originally PHP 5.3.3 source files
//

/*
   +----------------------------------------------------------------------+
   | PHP Version 5                                                        |
   +----------------------------------------------------------------------+
   | Copyright (c) 1997-2010 The PHP Group                                |
   +----------------------------------------------------------------------+
   | This source file is subject to version 3.01 of the PHP license,      |
   | that is bundled with this package in the file LICENSE, and is        |
   | available through the world-wide-web at the following url:           |
   | http://www.php.net/license/3_01.txt                                  |
   | If you did not receive a copy of the PHP license and are unable to   |
   | obtain it through the world-wide-web, please send a note to          |
   | license@php.net so we can mail you a copy immediately.               |
   +----------------------------------------------------------------------+
   | Author: Zeev Suraski <zeev@zend.com>                                 |
   +----------------------------------------------------------------------+
*/

/* $Id: php_open_temporary_file.h 293036 2010-01-03 09:23:27Z sebastian $ */

#ifndef PHP_OPEN_TEMPORARY_FILE_H
#define PHP_OPEN_TEMPORARY_FILE_H

BEGIN_EXTERN_C()
PHPAPI FILE *php_open_temporary_file(const char *dir, const char *pfx, char **opened_path_p TSRMLS_DC);
PHPAPI int php_open_temporary_fd_ex(const char *dir, const char *pfx, char **opened_path_p, zend_bool open_basedir_check TSRMLS_DC);
PHPAPI int php_open_temporary_fd(const char *dir, const char *pfx, char **opened_path_p TSRMLS_DC);
PHPAPI const char *php_get_temporary_directory(void);
PHPAPI void php_shutdown_temporary_directory(void);
END_EXTERN_C()

#endif /* PHP_OPEN_TEMPORARY_FILE_H */


