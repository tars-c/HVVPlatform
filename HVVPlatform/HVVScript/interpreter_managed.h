#pragma once

#include "macro.h"

#include <string>
#include <memory>
#include <list>
#include <map>
#include <functional>

#include "object.h"
#include "native_module.hpp"

namespace hv::v1{
	class pimpl_interpreter;
	class HVAPI_EXPORT interpreter_managed {
	private:
		std::shared_ptr<pimpl_interpreter> _pimpl;
	public:

		interpreter_managed();
		~interpreter_managed();

		bool set_module_path(std::string path);
		bool run_script(std::string content);
		bool run_file(std::string path);
		bool terminate();

		bool register_external_data(std::string key, std::shared_ptr<object> data);
		std::shared_ptr<object> external_data(std::string key);
		bool check_external_data(std::string key);
		void clear_external_data();

		std::list<std::string> global_names();
		std::map<std::string, std::shared_ptr<object>> global_objects();
		
		std::list<std::string> external_names();
		std::map<std::string, std::shared_ptr<object>> external_objects();

		std::shared_ptr<std::map<std::string, hv::v1::native_module>> native_modules();

		void set_trace_callback(std::function<void(char*)> _callback);
		void reset_trace_callback();

		/// <summary>
		/// script static functions
		/// </summary>
		static bool init_v8_startup_data(std::string path);
		static void init_v8_platform();
		static bool init_v8_engine();
		static void set_v8_flag(std::string flag);

	};
}